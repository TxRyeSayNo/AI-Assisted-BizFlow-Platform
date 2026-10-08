using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record ReviseRequestInput(
    string? Title = null,
    string? Description = null,
    string? Priority = null,
    bool SubmitImmediately = false);

public sealed record RequestRevisedView(
    Guid RequestId,
    Guid SourceRequestId,
    string Title,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record StoredRequestRevision(string Fingerprint, RequestRevisedView Result);

public interface IRequestReviseStore
{
    Task<IRequestReviseTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid sourceRequestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestReviseTransaction : IAsyncDisposable
{
    WorkRequest? SourceRequest { get; }
    UserAccount? Actor { get; }
    Task<StoredRequestRevision?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(WorkRequest revision, AuditLog audit, CancellationToken cancellationToken);
}

public sealed class RequestReviseCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestReviseStore store,
    TimeProvider clock)
{
    public async Task<RequestRevisedView> ReviseAsync(Guid sourceRequestId, ReviseRequestInput input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.create", new(context.TenantId), cancellationToken: cancellationToken);
        if (input.SubmitImmediately)
        {
            await authorizer.AuthorizeAsync("requests.submit", new(context.TenantId, context.UserId), cancellationToken: cancellationToken);
        }

        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            sourceRequestId,
            title = input.Title?.Trim(),
            description = input.Description?.Trim(),
            priority = input.Priority?.Trim().ToUpperInvariant(),
            submit = input.SubmitImmediately
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, sourceRequestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.create", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different revision input.");

        var source = transaction.SourceRequest;
        if (source is null || source.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.create", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        if (source.Status != RequestState.Rejected)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.NOT_REJECTED", "Only a rejected request can be revised.");

        var isTenantAdmin = access.Grants.Any(g => g.Code == "roles.manage" || (g.Code == "requests.create" && g.Scope == PermissionScope.Tenant));
        if (source.RequesterId != actorId && !isTenantAdmin)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.create", AccessDenial.ResourceNotFound, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Only the original requester can revise a rejected request.");
        }

        var priority = RequestPriority.Medium;
        if (!string.IsNullOrWhiteSpace(input.Priority))
        {
            if (!Enum.TryParse<RequestPriority>(input.Priority.Trim(), true, out var parsedPriority))
                throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_PRIORITY", "Invalid request priority.");
            priority = parsedPriority;
        }
        else
        {
            priority = source.Priority;
        }

        var title = string.IsNullOrWhiteSpace(input.Title) ? source.Title : input.Title.Trim();
        var description = string.IsNullOrWhiteSpace(input.Description) ? source.Description : input.Description.Trim();
        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());

        var revision = WorkRequest.ReviseRejected(source, actorId, source.ServiceId, source.CategoryId, title, description, now, priority);
        if (input.SubmitImmediately)
        {
            revision.ApplySubmissionTransition(new RequestWorkflowMutation(actorId, RequestState.Draft, RequestState.Submitted, revision.UpdatedAt), now);
        }

        var audit = AuditLog.RequestRevised(source, revision, now, keyHash, keyHash is null ? null : fingerprint);

        await transaction.CommitAsync(revision, audit, cancellationToken);

        return new(
            revision.Id,
            source.Id,
            revision.Title,
            revision.Status.ToString().ToUpperInvariant(),
            now);
    }
}
