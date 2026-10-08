using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RequestCancelledView(Guid RequestId, string Status, DateTimeOffset CancelledAt);

public sealed record StoredRequestCancellation(string Fingerprint, RequestCancelledView Result);

public interface IRequestCancellationStore
{
    Task<IRequestCancellationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestCancellationTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    Task<StoredRequestCancellation?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(AuditLog audit, CancellationToken cancellationToken);
}

public sealed class RequestCancellationCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestCancellationStore store,
    TimeProvider clock)
{
    public async Task<RequestCancelledView> CancelAsync(Guid requestId, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.cancel", new(context.TenantId, context.UserId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, requestId }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.cancel", new(tenantId, actorId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different cancellation input.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.cancel", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var decision = RequestLifecyclePolicy.EvaluateUser(request.Status, RequestState.Cancelled, access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, IsRequester: request.RequesterId == actorId));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.cancel", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to cancel this request.");
        }

        if (!decision.Allowed)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be cancelled in its current state.");

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        request.ApplyCancellationTransition(new(actorId, request.Status, RequestState.Cancelled, request.UpdatedAt), now);

        var audit = AuditLog.RequestCancelled(request, now, keyHash, keyHash is null ? null : fingerprint);
        await transaction.CommitAsync(audit, cancellationToken);

        return new(request.Id, RequestListCodes.State(request.Status), now);
    }
}
