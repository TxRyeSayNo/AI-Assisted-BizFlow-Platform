using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.Application.Tasks;

public sealed record TaskAcceptedView(Guid TaskId, Guid AssignmentId, Guid ConfirmationId, string Status, DateTimeOffset AcceptedAt);
public sealed record StoredTaskAcceptance(string Fingerprint, TaskAcceptedView Result);
public interface ITaskAcceptanceStore
{
    Task<ITaskAcceptanceTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken);
}
public interface ITaskAcceptanceTransaction : IAsyncDisposable
{
    WorkTask? Task { get; }
    TaskAssignment? Assignment { get; }
    UserAccount? Actor { get; }
    Task<StoredTaskAcceptance?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken);
}

public sealed class TaskAcceptanceCommand(ITenantContext context, IResourceAuthorizer authorizer, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit, ITaskAcceptanceStore store, IWorkflowEngine workflow, TimeProvider clock)
{
    public async Task<TaskAcceptedView> AcceptAsync(Guid taskId, string? inputNote, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.accept", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId) throw ApplicationFault.NotFound();
        var note = string.IsNullOrWhiteSpace(inputNote) ? null : inputNote.Trim();
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a plain-text acceptance note.");
        var keyHash = TaskCreationRules.KeyHash(key);
        var fingerprint = TaskCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, taskId, note }));
        await using var transaction = await store.BeginAsync(tenantId, actorId, taskId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("tasks.accept", new(tenantId), cancellationToken: cancellationToken);
        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different acceptance input.");
        var task = transaction.Task; var assignment = transaction.Assignment; var actor = transaction.Actor;
        if (task is null || assignment is null || actor is null ||
            (assignment.UserId is { } user ? user != actorId : assignment.DepartmentId is null || assignment.DepartmentId != actor.DepartmentId))
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.accept", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        if (replay is not null) return replay.Result;
        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId) throw ApplicationFault.NotFound();
        var now = TaskCreationRules.DatabaseTime(clock.GetUtcNow());
        var decision = workflow.AcceptTask(task, assignment, actor, access, now);
        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.accept", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You no longer have access to this acceptance.");
        }
        if (!decision.Allowed) throw new ApplicationFault(FaultKind.Conflict, "TASK.TRANSITION_DENIED", "This assignment cannot be accepted in its current state or configuration.");
        var confirmation = Confirmation.TaskAccepted(task, assignment, actorId, note);
        var audit = AuditLog.TaskAccepted(task, assignment, confirmation, keyHash, keyHash is null ? null : fingerprint);
        var notification = Notification.Create(tenantId, assignment.AssignedBy, NotificationEvent.TaskAccepted, "Task accepted", task.Title,
            $"task-accepted/{assignment.Id:N}/{assignment.AssignedBy:N}", "Task", task.Id);
        await transaction.CommitAsync(confirmation, audit, notification, cancellationToken);
        return new(task.Id, assignment.Id, confirmation.Id, "ACCEPTED", now);
    }
}
