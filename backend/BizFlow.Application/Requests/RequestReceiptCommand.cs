using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Requests;

public sealed record ReceiveRequestInput(string? Note = null);

public sealed record RequestReceivedView(
    Guid RequestId,
    Guid ConfirmationId,
    string Status,
    DateTimeOffset ReceivedAt);

public sealed record StoredRequestReceipt(string Fingerprint, RequestReceivedView Result);

public interface IRequestReceiptStore
{
    Task<IRequestReceiptTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestReceiptTransaction : IAsyncDisposable
{
    WorkRequest? Request { get; }
    RequestRouting? LatestRouting { get; }
    Task<bool> IsUserInDepartmentAsync(Guid userId, Guid departmentId, CancellationToken cancellationToken);
    Task<StoredRequestReceipt?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken);
}

public sealed class RequestReceiptCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit,
    IRequestReceiptStore store,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestReceivedView> ReceiveAsync(Guid requestId, ReceiveRequestInput input, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.receive", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var keyHash = RequestCreationRules.KeyHash(key);
        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            requestId,
            note = input.Note?.Trim()
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("requests.receive", new(tenantId), cancellationToken: cancellationToken);

        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different receipt input.");

        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.receive", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        if (replay is not null) return replay.Result;

        var routing = transaction.LatestRouting;
        var isAssignedTarget = false;
        if (routing is not null)
        {
            if (routing.ToUserId == actorId)
            {
                isAssignedTarget = true;
            }
            else if (routing.ToDepartmentId is { } deptId)
            {
                isAssignedTarget = await transaction.IsUserInDepartmentAsync(actorId, deptId, cancellationToken);
            }
        }

        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId)
            throw ApplicationFault.NotFound();

        var decision = RequestLifecyclePolicy.EvaluateUser(request.Status, RequestState.Received, access,
            new(request.TenantId, request.RequesterId),
            new(WorkflowAllowsTransition: true, IsAssignedTarget: isAssignedTarget));

        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "requests.receive", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have permission to receive this request.");
        }

        if (!decision.Allowed)
        {
            if (decision.Denial == RequestTransitionDenial.AssignedTargetRequired)
                throw new ApplicationFault(FaultKind.Forbidden, "REQUEST.NOT_ASSIGNED_TARGET", "Only the assigned user or a member of the assigned department can receive this request.");
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TRANSITION_DENIED", "This request cannot be received in its current state.");
        }

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        request.ApplyReceiptTransition(new(actorId, RequestState.Routed, RequestState.Received, request.UpdatedAt), now);

        var confirmation = Confirmation.RequestReceived(request, actorId, input.Note);
        var audit = AuditLog.RequestReceived(request, confirmation, now, keyHash, keyHash is null ? null : fingerprint);
        var notification = Notification.Create(
            tenantId,
            request.RequesterId,
            NotificationEvent.RequestReceived,
            "Request received",
            $"Your request '{request.Title}' has been received and accepted for processing.",
            $"request-receive/{confirmation.Id:N}",
            "Request",
            request.Id);

        await transaction.CommitAsync(confirmation, audit, notification, cancellationToken);
        await updates.PublishAsync(tenantId, [request.RequesterId], cancellationToken);

        return new(request.Id, confirmation.Id, RequestListCodes.State(request.Status), now);
    }
}
