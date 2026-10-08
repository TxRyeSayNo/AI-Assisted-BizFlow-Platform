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

public sealed record TaskConfirmedView(Guid TaskId, Guid ConfirmationId, string Decision, string Status, DateTimeOffset ConfirmedAt);
public sealed record StoredTaskConfirmation(string Fingerprint, TaskConfirmedView Result);

public interface ITaskConfirmationStore
{
    Task<ITaskConfirmationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken);
}

public interface ITaskConfirmationTransaction : IAsyncDisposable
{
    WorkTask? Task { get; }
    TaskAssignment? Assignment { get; }
    UserAccount? Actor { get; }
    Task<StoredTaskConfirmation?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken);
}

public sealed class TaskConfirmationCommand(ITenantContext context, IResourceAuthorizer authorizer, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit, ITaskConfirmationStore store, IWorkflowEngine workflow, TimeProvider clock, BizFlow.Application.Notifications.INotificationUpdates updates)
{
    public async Task<TaskConfirmedView> ConfirmAsync(Guid taskId, string decisionText, string? inputNote, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.confirm", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId) throw ApplicationFault.NotFound();
        var normalizedDecision = decisionText?.Trim().ToUpperInvariant();
        if (normalizedDecision is not ("CONFIRM" or "CONFIRMED" or "REWORK" or "REJECT" or "REJECTED"))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Decision must be CONFIRMED or REWORK.");
        var accept = normalizedDecision is "CONFIRM" or "CONFIRMED";
        var note = string.IsNullOrWhiteSpace(inputNote) ? null : inputNote.Trim();
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use plain text for review note.");
        if (!accept && string.IsNullOrWhiteSpace(note))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A reason note is required when requesting rework.");
        var keyHash = TaskCreationRules.KeyHash(key);
        var fingerprint = TaskCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, taskId, accept, note }));
        await using var transaction = await store.BeginAsync(tenantId, actorId, taskId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("tasks.confirm", new(tenantId), cancellationToken: cancellationToken);
        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with a different confirmation decision.");
        var task = transaction.Task; var assignment = transaction.Assignment; var actor = transaction.Actor;
        if (task is null || assignment is null || actor is null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.confirm", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        if (replay is not null) return replay.Result;
        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId) throw ApplicationFault.NotFound();
        var now = TaskCreationRules.DatabaseTime(clock.GetUtcNow());
        var decision = workflow.ConfirmResult(task, assignment, actor, access, accept, now);
        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.confirm", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You no longer have access to review this work.");
        }
        if (!decision.Allowed) throw new ApplicationFault(FaultKind.Conflict, "TASK.TRANSITION_DENIED", "This result cannot be reviewed in its current state or configuration.");
        var confirmation = accept ? Confirmation.TaskResultConfirmed(task, actorId, note) : Confirmation.TaskResultRejected(task, actorId, note);
        var audit = accept ? AuditLog.TaskResultConfirmed(task, assignment, confirmation, keyHash, keyHash is null ? null : fingerprint)
                            : AuditLog.TaskResultRework(task, assignment, confirmation, keyHash, keyHash is null ? null : fingerprint);
        var recipientId = assignment.UserId ?? actorId;
        var notifType = NotificationEvent.TaskConfirmed;
        var notifTitle = accept ? "Task result confirmed" : "Task rework requested";
        var notifContent = accept ? $"Result for task '{task.Title}' was approved." : $"Result for task '{task.Title}' requires rework: {note}";
        var notifKey = accept ? $"task-confirmed/{task.Id:N}/{confirmation.Id:N}" : $"task-rework/{task.Id:N}/{confirmation.Id:N}";
        var notification = Notification.Create(tenantId, recipientId, notifType, notifTitle, notifContent, notifKey, "Task", task.Id);
        await transaction.CommitAsync(confirmation, audit, notification, cancellationToken);
        await updates.PublishAsync(tenantId, [recipientId], cancellationToken);
        var statusText = JsonNamingPolicy.SnakeCaseUpper.ConvertName(task.Status.ToString());
        return new(task.Id, confirmation.Id, accept ? "CONFIRMED" : "REWORK", statusText, now);
    }
}
