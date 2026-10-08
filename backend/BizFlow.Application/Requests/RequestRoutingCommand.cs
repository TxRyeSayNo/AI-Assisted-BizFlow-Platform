using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RouteRequestInput(
    Guid? ToDepartmentId,
    Guid? ToUserId = null,
    string? Reason = null,
    RequestRoutingSource Source = RequestRoutingSource.Manual);

public sealed record RequestRoutedView(
    Guid RequestId,
    Guid RoutingId,
    string Status,
    Guid? ToDepartmentId,
    Guid? ToUserId,
    DateTimeOffset RoutedAt);

public sealed record StoredRequestRouting(string Fingerprint, RequestRoutedView Result);

public interface IRequestRoutingStore
{
    Task<IRequestRoutingTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestRoutingTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    RequestRouting? LatestRouting { get; }
    Task<bool> DepartmentExistsAndActiveAsync(Guid departmentId, CancellationToken cancellationToken);
    Task<bool> UserExistsAndActiveAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetDepartmentManagersAsync(Guid departmentId, CancellationToken cancellationToken);
    Task<StoredRequestRouting?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(RequestRouting routing, AuditLog audit, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken);
}

public sealed class RequestRoutingCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestRoutingStore store,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestRoutedView> RouteAsync(Guid requestId, RouteRequestInput input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.route", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        if (input.ToDepartmentId is null && input.ToUserId is null)
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.TARGET_REQUIRED", "At least one target department or user is required.");

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            requestId,
            toDepartmentId = input.ToDepartmentId,
            toUserId = input.ToUserId,
            reason = input.Reason?.Trim(),
            source = input.Source.ToString().ToUpperInvariant()
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.route", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different routing input.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.route", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        if (input.ToDepartmentId is { } deptId)
        {
            var validDept = await transaction.DepartmentExistsAndActiveAsync(deptId, cancellationToken);
            if (!validDept)
                throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_TARGET", "The target department is invalid or inactive.");
        }

        if (input.ToUserId is { } targetUser)
        {
            var validUser = await transaction.UserExistsAndActiveAsync(targetUser, cancellationToken);
            if (!validUser)
                throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_TARGET", "The target user is invalid or inactive.");
        }

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var decision = RequestLifecyclePolicy.EvaluateUser(request.Status, RequestState.Routed, access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, TargetValidated: true),
            input.ToDepartmentId);

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.route", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to route this request.");
        }

        if (!decision.Allowed)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be routed in its current state.");

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        request.ApplyRoutingTransition(new(actorId, request.Status, RequestState.Routed, request.UpdatedAt), now);

        var previous = transaction.LatestRouting;
        var routing = RequestRouting.Create(
            tenantId,
            request.Id,
            actorId,
            now,
            input.ToDepartmentId,
            input.ToUserId,
            previous?.ToDepartmentId,
            previous?.ToUserId,
            input.Reason,
            input.Source);

        var audit = AuditLog.RequestRouted(request, routing, now, keyHash, keyHash is null ? null : fingerprint);

        var recipientIds = new List<Guid>();
        if (input.ToUserId is { } tu)
        {
            recipientIds.Add(tu);
        }
        else if (input.ToDepartmentId is { } td)
        {
            var managers = await transaction.GetDepartmentManagersAsync(td, cancellationToken);
            recipientIds.AddRange(managers);
        }

        var notifications = recipientIds.Distinct()
            .Select(recipientId => Notification.Create(
                tenantId,
                recipientId,
                NotificationEvent.RequestRouted,
                "Request routed",
                $"Request '{request.Title}' has been routed to your department/queue.",
                $"request-route/{routing.Id:N}",
                "Request",
                request.Id))
            .ToList();

        await transaction.CommitAsync(routing, audit, notifications, cancellationToken);
        if (recipientIds.Count > 0)
        {
            await updates.PublishAsync(tenantId, recipientIds, cancellationToken);
        }

        return new(request.Id, routing.Id, RequestListCodes.State(request.Status), routing.ToDepartmentId, routing.ToUserId, now);
    }
}
