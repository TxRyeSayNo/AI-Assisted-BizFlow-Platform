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

public sealed record ResolveRequestInput(string Content);

public sealed record RequestResolvedView(
    Guid RequestId,
    Guid ResolutionId,
    int RevisionNo,
    string Content,
    string Status,
    DateTimeOffset ResolvedAt);

public sealed record StoredRequestResolution(string Fingerprint, RequestResolvedView Result);

public interface IRequestResolutionStore
{
    Task<IRequestResolutionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestResolutionTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    RequestRouting? LatestRouting { get; }
    UserAccount? Actor { get; }
    Task<int> NextRevisionNoAsync(CancellationToken cancellationToken);
    Task<StoredRequestResolution?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(RequestResolution resolution, Action applyTransition, Func<AuditLog> auditFactory, Notification notification, CancellationToken cancellationToken);
}

public sealed class RequestResolutionCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestResolutionStore store,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestResolvedView> ResolveAsync(Guid requestId, ResolveRequestInput input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.resolve", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        if (string.IsNullOrWhiteSpace(input.Content))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.CONTENT_REQUIRED", "Resolution content is required.");

        var text = input.Content.Trim();
        if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_CONTENT", "Resolution content must be plain text.");

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            requestId,
            content = text
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.resolve", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different resolution content.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.resolve", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var decision = RequestLifecyclePolicy.EvaluateUser(
            request.Status,
            RequestState.Resolved,
            access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, HasResolution: true));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.resolve", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to resolve this request.");
        }

        if (!decision.Allowed)
        {
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", $"Request cannot be resolved from state '{request.Status}'.");
        }

        var isTenantAdmin = access.Grants.Any(g => g.Code == "roles.manage" || (g.Code == "requests.resolve" && g.Scope == PermissionScope.Tenant));
        var routing = transaction.LatestRouting;
        var isAuthorizedResolver = isTenantAdmin;
        if (!isAuthorizedResolver && routing is not null)
        {
            if (routing.ToUserId == actorId)
            {
                isAuthorizedResolver = true;
            }
            else if (routing.ToDepartmentId is { } deptId)
            {
                isAuthorizedResolver = access.DepartmentIds.Contains(deptId) || access.ManagedDepartmentIds.Contains(deptId);
            }
        }
        else if (!isAuthorizedResolver)
        {
            isAuthorizedResolver = access.ManagedDepartmentIds.Count > 0;
        }

        if (!isAuthorizedResolver)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.resolve", AccessDenial.ManagementScopeDenied, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You are not the designated recipient or department resolver for this request.");
        }

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        var revisionNo = await transaction.NextRevisionNoAsync(cancellationToken);
        var resolution = RequestResolution.Create(request.Id, actorId, text, revisionNo, now);
        var mutation = new RequestWorkflowMutation(actorId, RequestState.InProgress, RequestState.Resolved, request.UpdatedAt);

        var notificationKey = $"request-resolved/{request.Id:N}/{resolution.Id:N}";
        var notification = Notification.Create(
            tenantId,
            request.RequesterId,
            NotificationEvent.RequestResolved,
            "Request resolved",
            $"Request '{request.Title}' has been resolved and is ready for your confirmation.",
            notificationKey,
            "Request",
            request.Id);

        await transaction.CommitAsync(
            resolution,
            () => request.ApplyResolutionTransition(mutation, now),
            () => AuditLog.RequestResolved(request, resolution, now, keyHash, keyHash is null ? null : fingerprint),
            notification,
            cancellationToken);

        await updates.PublishAsync(tenantId, [request.RequesterId], cancellationToken);

        return new(
            request.Id,
            resolution.Id,
            resolution.RevisionNo,
            resolution.Content,
            "RESOLVED",
            now);
    }
}
