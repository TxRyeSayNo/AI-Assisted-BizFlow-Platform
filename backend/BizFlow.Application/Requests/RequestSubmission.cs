using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RequestSubmittedView(Guid RequestId, string Status, DateTimeOffset SubmittedAt);
public sealed record StoredRequestSubmission(string Fingerprint, RequestSubmittedView Result);

public interface IRequestSubmissionStore
{
    Task<IRequestSubmissionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestSubmissionTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    Task<StoredRequestSubmission?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(AuditLog audit, Notification notification, CancellationToken cancellationToken);
}

public sealed class RequestSubmissionCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestSubmissionStore store,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestSubmittedView> SubmitAsync(Guid requestId, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.submit", new(context.TenantId, context.UserId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, requestId }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.submit", new(tenantId, actorId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different submission input.");

        var request = transaction.Request;
        if (request is null || request.RequesterId != actorId || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.submit", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        var decision = RequestLifecyclePolicy.EvaluateUser(request.Status, RequestState.Submitted, access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, IsRequester: true));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.submit", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to submit this request.");
        }

        if (!decision.Allowed)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be submitted in its current state.");

        request.ApplySubmissionTransition(new(actorId, RequestState.Draft, RequestState.Submitted, request.UpdatedAt), now);
        var audit = AuditLog.RequestSubmitted(request, now, keyHash, keyHash is null ? null : fingerprint);
        var notification = Notification.Create(tenantId, request.RequesterId, NotificationEvent.RequestSubmitted,
            "Request submitted", request.Title, $"request-submit/{request.Id:N}", "Request", request.Id);

        await transaction.CommitAsync(audit, notification, cancellationToken);
        await updates.PublishAsync(tenantId, [notification.RecipientId], cancellationToken);

        return new(request.Id, RequestListCodes.State(request.Status), now);
    }
}
