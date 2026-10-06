using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;
using System.Text.Json;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Tasks;

public sealed record AssignTaskCommand(string TargetType, Guid TargetId, string? Note = null);
public sealed record TaskAssignedView(Guid TaskId, Guid AssignmentId, string Status, DateTimeOffset AssignedAt);
public sealed record StoredTaskAssignment(string Fingerprint, TaskAssignedView Result);
public sealed record AssignmentTarget(Guid? UserId, Guid? DepartmentId, Guid ScopeDepartmentId, IReadOnlyList<Guid> Recipients);
public interface ITaskAssignmentStore
{
    Task<ITaskAssignmentTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken);
}
public interface ITaskAssignmentTransaction : IAsyncDisposable
{
    WorkTask? Task { get; }
    TaskAssignment? CurrentAssignment { get; }
    Task<AssignmentTarget?> LoadTargetAsync(string type, Guid id, bool requireActive, CancellationToken cancellationToken);
    Task<StoredTaskAssignment?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(TaskAssignment assignment, AuditLog audit, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken);
}

public sealed class TaskAssignmentCommand(ITenantContext context, IResourceAuthorizer authorizer, IAccessSnapshotProvider snapshots, ISecurityAuditWriter securityAudit,
    ITaskAssignmentStore store, IWorkflowEngine workflow, TimeProvider clock)
{
    public async Task<TaskAssignedView> AssignAsync(Guid taskId, AssignTaskCommand input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.assign", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId) throw ApplicationFault.NotFound();
        if (input.TargetType is not ("USER" or "DEPARTMENT") || input.TargetId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_TARGET", "Select an employee or department target.");
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a plain-text assignment note.");
        var keyHash = TaskCreationRules.KeyHash(key);
        var fingerprint = TaskCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, taskId, input.TargetType, input.TargetId, note }));
        await using var transaction = await store.BeginAsync(tenantId, actorId, taskId, keyHash, cancellationToken);
        // Look up replay before inspecting current task state, but never before live authority.
        await authorizer.AuthorizeAsync("tasks.assign", new(tenantId), cancellationToken: cancellationToken);
        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different assignment input.");
        var task = transaction.Task;
        var target = task is null ? null : await transaction.LoadTargetAsync(input.TargetType, input.TargetId, replay is null, cancellationToken);
        if (task is null || target is null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.assign", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        await authorizer.AuthorizeAsync("tasks.assign", new(task.TenantId, task.CreatorId), target.ScopeDepartmentId, cancellationToken);
        if (replay is not null) return replay.Result;
        if (task.Status == TaskState.Draft && transaction.CurrentAssignment is not null ||
            task.Status == TaskState.Rejected && (transaction.CurrentAssignment is null || transaction.CurrentAssignment.RejectedAt is null))
            throw new ApplicationFault(FaultKind.Conflict, "TASK.ASSIGNMENT_CONFLICT", "The current assignment does not match the task state.");
        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId) throw ApplicationFault.NotFound();
        var now = TaskCreationRules.DatabaseTime(clock.GetUtcNow());
        var decision = workflow.AssignTask(task, access, target.ScopeDepartmentId, true, now);
        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.assign", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You no longer have access to this assignment.");
        }
        if (!decision.Allowed)
            throw new ApplicationFault(FaultKind.Conflict, "TASK.TRANSITION_DENIED", "This task cannot be assigned in its current state or configuration.");
        var assignment = TaskAssignment.Create(task.Id, actorId, target.DepartmentId, target.UserId, now);
        var audit = AuditLog.TaskAssigned(task, assignment, note, now, keyHash, keyHash is null ? null : fingerprint);
        var notifications = target.Recipients.Distinct().Select(recipient => Notification.Create(tenantId, recipient,
            NotificationEvent.TaskAssigned, "Task assigned", task.Title, $"task-assigned/{assignment.Id:N}/{recipient:N}", "Task", task.Id)).ToArray();
        await transaction.CommitAsync(assignment, audit, notifications, cancellationToken);
        return new(task.Id, assignment.Id, "ASSIGNED", now);
    }
}
