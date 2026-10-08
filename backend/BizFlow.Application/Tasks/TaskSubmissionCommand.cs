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

public sealed record TaskSubmittedView(Guid TaskId, Guid TaskResultId, int RevisionNo, string Status, DateTimeOffset SubmittedAt);
public sealed record StoredTaskSubmission(string Fingerprint, TaskSubmittedView Result);

public interface ITaskSubmissionStore
{
    Task<ITaskSubmissionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken);
}

public interface ITaskSubmissionTransaction : IAsyncDisposable
{
    WorkTask? Task { get; }
    TaskAssignment? Assignment { get; }
    UserAccount? Actor { get; }
    int NextRevisionNo { get; }
    Task<StoredTaskSubmission?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(TaskResult result, AuditLog audit, Notification notification, CancellationToken cancellationToken);
}

public sealed class TaskSubmissionCommand(ITenantContext context, IResourceAuthorizer authorizer, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit, ITaskSubmissionStore store, IWorkflowEngine workflow, TimeProvider clock, BizFlow.Application.Notifications.INotificationUpdates updates)
{
    public async Task<TaskSubmittedView> SubmitAsync(Guid taskId, string? inputContent, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.submit", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId) throw ApplicationFault.NotFound();
        var content = string.IsNullOrWhiteSpace(inputContent) ? null : inputContent.Trim();
        if (content?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use plain text for result content.");
        var keyHash = TaskCreationRules.KeyHash(key);
        var fingerprint = TaskCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, taskId, content }));
        await using var transaction = await store.BeginAsync(tenantId, actorId, taskId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("tasks.submit", new(tenantId), cancellationToken: cancellationToken);
        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different submission input.");
        var task = transaction.Task; var assignment = transaction.Assignment; var actor = transaction.Actor;
        if (task is null || assignment is null || actor is null || assignment.UserId != actorId)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.submit", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        if (replay is not null) return replay.Result;
        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId) throw ApplicationFault.NotFound();
        var now = TaskCreationRules.DatabaseTime(clock.GetUtcNow());
        var decision = workflow.SubmitResult(task, assignment, actor, access, now);
        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.submit", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You no longer have access to submit this work.");
        }
        if (!decision.Allowed) throw new ApplicationFault(FaultKind.Conflict, "TASK.TRANSITION_DENIED", "This task cannot be submitted in its current state or configuration.");
        var result = TaskResult.CreateSnapshot(task.Id, actorId, content, transaction.NextRevisionNo, now);
        var audit = AuditLog.TaskResultSubmitted(task, assignment, result, keyHash, keyHash is null ? null : fingerprint);
        var notification = Notification.Create(tenantId, assignment.AssignedBy, NotificationEvent.ResultSubmitted, "Result submitted", task.Title,
            $"task-result/{task.Id:N}/{result.RevisionNo}", "Task", task.Id);
        await transaction.CommitAsync(result, audit, notification, cancellationToken);
        await updates.PublishAsync(tenantId, [notification.RecipientId], cancellationToken);
        return new(task.Id, result.Id, result.RevisionNo, "SUBMITTED", now);
    }
}
