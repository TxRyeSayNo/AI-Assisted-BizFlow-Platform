using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RejectRequestInput(string Reason);

public sealed record RequestRejectedView(
    Guid RequestId,
    string Status,
    string Reason,
    DateTimeOffset RejectedAt);

public sealed record StoredRequestRejection(string Fingerprint, RequestRejectedView Result);

public interface IRequestRejectionStore
{
    Task<IRequestRejectionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestRejectionTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    Task<StoredRequestRejection?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(AuditLog audit, Notification notification, CancellationToken cancellationToken);
}

public sealed class RequestRejectionCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestRejectionStore store,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestRejectedView> RejectAsync(Guid requestId, RejectRequestInput input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.reject", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.REASON_REQUIRED", "A plain-text rejection reason is required.");

        var text = input.Reason.Trim();
        if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_REASON", "Rejection reason must be plain text.");

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            requestId,
            reason = text
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.reject", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different rejection input.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.reject", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var decision = RequestLifecyclePolicy.EvaluateUser(request.Status, RequestState.Rejected, access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, Reason: text));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.reject", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to reject this request.");
        }

        if (!decision.Allowed)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be rejected in its current state.");

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        request.ApplyRejectionTransition(new(actorId, request.Status, RequestState.Rejected, request.UpdatedAt), now);

        var audit = AuditLog.RequestRejected(request, text, now, keyHash, keyHash is null ? null : fingerprint);
        var notification = Notification.Create(
            tenantId,
            request.RequesterId,
            NotificationEvent.RequestRejected,
            "Request rejected",
            $"Your request '{request.Title}' has been rejected. Reason: {text}",
            $"request-reject/{request.Id:N}",
            "Request",
            request.Id);

        await transaction.CommitAsync(audit, notification, cancellationToken);
        await updates.PublishAsync(tenantId, [request.RequesterId], cancellationToken);

        return new(request.Id, RequestListCodes.State(request.Status), text, now);
    }
}
