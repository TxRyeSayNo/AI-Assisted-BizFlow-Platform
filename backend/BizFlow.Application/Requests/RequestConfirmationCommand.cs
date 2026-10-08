using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Requests;

public sealed record ConfirmRequestInput(string Decision, string? Note = null);

public sealed record RequestConfirmedView(
    Guid RequestId,
    Guid ConfirmationId,
    string Decision,
    string Status,
    DateTimeOffset ConfirmedAt);

public sealed record StoredRequestConfirmation(string Fingerprint, RequestConfirmedView Result);

public interface IRequestConfirmationStore
{
    Task<IRequestConfirmationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestConfirmationTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    RequestRouting? LatestRouting { get; }
    UserAccount? Actor { get; }
    Task<StoredRequestConfirmation?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(
        Confirmation confirmation,
        AuditLog audit,
        Notification notification,
        Action? applyClosure,
        Func<AuditLog>? closeAuditFactory,
        CancellationToken cancellationToken);
}

public sealed class RequestConfirmationCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestConfirmationStore store,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestConfirmedView> ConfirmAsync(Guid requestId, ConfirmRequestInput input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.confirm", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var normalizedDecision = input.Decision?.Trim().ToUpperInvariant();
        if (normalizedDecision is not ("CONFIRM" or "CONFIRMED" or "REWORK" or "REJECT" or "REJECTED"))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_DECISION", "Decision must be CONFIRMED or REWORK.");

        var accept = normalizedDecision is "CONFIRM" or "CONFIRMED";
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_NOTE", "Use plain text for review note.");

        if (!accept && string.IsNullOrWhiteSpace(note))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.REWORK_REASON_REQUIRED", "A reason note is required when requesting rework.");

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            requestId,
            accept,
            note
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.confirm", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with a different confirmation decision.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.confirm", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var isRequester = request.RequesterId == actorId;
        var decision = RequestLifecyclePolicy.EvaluateUser(
            request.Status,
            accept ? RequestState.Confirmed : RequestState.InProgress,
            access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, IsRequester: isRequester));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.confirm", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to review this request.");
        }

        if (!decision.Allowed)
        {
            if (decision.Denial == RequestTransitionDenial.RequesterRequired)
                throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Only the requester can confirm or request rework for this request.");
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be confirmed or returned for rework in its current state.");
        }

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        var routing = transaction.LatestRouting;
        var resolverRecipientId = routing?.ToUserId ?? (routing?.ToDepartmentId is not null ? actorId : actorId);

        if (accept)
        {
            // Transition: RESOLVED -> CONFIRMED
            request.ApplyConfirmationTransition(new RequestWorkflowMutation(actorId, RequestState.Resolved, RequestState.Confirmed, request.UpdatedAt), now);
            var confirmation = Confirmation.RequestResolutionConfirmed(request, actorId, note);
            var audit = AuditLog.RequestConfirmed(request, confirmation, now, keyHash, keyHash is null ? null : fingerprint);

            var notification = Notification.Create(
                tenantId,
                resolverRecipientId,
                NotificationEvent.RequestResolved,
                "Request resolution confirmed",
                $"Resolution for request '{request.Title}' was confirmed and closed by the requester.",
                $"request-confirmed/{request.Id:N}/{confirmation.Id:N}",
                "Request",
                request.Id);

            Action applyClosure = () =>
            {
                request.ResetWorkflowMutation();
                request.ApplyClosureTransition(new RequestWorkflowMutation(actorId, RequestState.Confirmed, RequestState.Closed, request.UpdatedAt), now);
            };

            await transaction.CommitAsync(
                confirmation,
                audit,
                notification,
                applyClosure,
                () => AuditLog.RequestClosed(request, now),
                cancellationToken);

            await updates.PublishAsync(tenantId, [resolverRecipientId], cancellationToken);

            return new(request.Id, confirmation.Id, "CONFIRMED", "CLOSED", now);
        }
        else
        {
            // Transition: RESOLVED -> IN_PROGRESS (rework)
            request.ApplyConfirmationTransition(new RequestWorkflowMutation(actorId, RequestState.Resolved, RequestState.InProgress, request.UpdatedAt), now);
            var confirmation = Confirmation.RequestResolutionRejected(request, actorId, note);
            var audit = AuditLog.RequestRework(request, confirmation, now, keyHash, keyHash is null ? null : fingerprint);

            var notification = Notification.Create(
                tenantId,
                resolverRecipientId,
                NotificationEvent.RequestResolved,
                "Request rework requested",
                $"Resolution for request '{request.Title}' requires rework: {note}",
                $"request-rework/{request.Id:N}/{confirmation.Id:N}",
                "Request",
                request.Id);

            await transaction.CommitAsync(confirmation, audit, notification, null, null, cancellationToken);
            await updates.PublishAsync(tenantId, [resolverRecipientId], cancellationToken);

            return new(request.Id, confirmation.Id, "REWORK", "IN_PROGRESS", now);
        }
    }
}
