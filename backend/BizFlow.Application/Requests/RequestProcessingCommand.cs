using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RequestProcessingView(Guid RequestId, string Status, DateTimeOffset StartedAt);

public sealed record StoredRequestProcessing(string Fingerprint, RequestProcessingView Result);

public interface IRequestProcessingStore
{
    Task<IRequestProcessingTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestProcessingTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    Task<StoredRequestProcessing?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(AuditLog audit, CancellationToken cancellationToken);
}

public sealed class RequestProcessingCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestProcessingStore store,
    TimeProvider clock)
{
    public async Task<RequestProcessingView> StartAsync(Guid requestId, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.process", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, requestId }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.process", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different processing input.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.process", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var decision = RequestLifecyclePolicy.EvaluateUser(request.Status, RequestState.InProgress, access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.process", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to process this request.");
        }

        if (!decision.Allowed)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be transitioned to InProgress in its current state.");

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        request.ApplyExecutionTransition(new(actorId, request.Status, RequestState.InProgress, request.UpdatedAt), now);

        var audit = AuditLog.RequestExecutionStarted(request, now, keyHash, keyHash is null ? null : fingerprint);
        await transaction.CommitAsync(audit, cancellationToken);

        return new(request.Id, RequestListCodes.State(request.Status), now);
    }
}
