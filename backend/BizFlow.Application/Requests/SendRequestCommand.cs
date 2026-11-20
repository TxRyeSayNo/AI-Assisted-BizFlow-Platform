using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;

namespace BizFlow.Application.Requests;

public sealed record SendRequestInput(Guid? ToDepartmentId, Guid? ToUserId, string? Reason = null);

// Lets the REQUESTER route their own submitted request to a chosen recipient (person or department),
// constrained by "no level-skipping": a recipient user may be at most one authority level above the
// requester. This is a separate, requester-owned path and does not change the manager routing command.
public sealed class SendRequestCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IRequestRoutingStore store,
    IRequestRecipientStore recipients,
    TimeProvider clock,
    INotificationUpdates updates)
{
    public async Task<RequestRoutedView> SendAsync(Guid requestId, SendRequestInput input, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.submit", new(context.TenantId, context.UserId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();
        if (input.ToDepartmentId is null && input.ToUserId is null)
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.TARGET_REQUIRED", "Vui lòng chọn nơi nhận yêu cầu.");
        if (input.ToDepartmentId is not null && input.ToUserId is not null)
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.TARGET_AMBIGUOUS", "Chỉ chọn một nơi nhận: phòng ban hoặc người.");

        await using var transaction = await store.BeginAsync(tenantId, actorId, requestId, null, cancellationToken);
        var request = transaction.Request;
        if (request is null || request.DeletedAt is not null)
            throw ApplicationFault.NotFound();

        // Only the requester may send their own request this way; this is what keeps employees from
        // routing other people's work, and the level check below is what blocks skipping a level.
        if (request.RequesterId != actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "REQUEST.NOT_OWNER", "Bạn chỉ có thể chọn nơi nhận cho yêu cầu của chính mình.");
        if (request.Status != RequestState.Submitted)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.NOT_SENDABLE", "Chỉ có thể chọn nơi nhận cho yêu cầu vừa gửi (chưa định tuyến).");

        if (input.ToDepartmentId is { } deptId && !await transaction.DepartmentExistsAndActiveAsync(deptId, cancellationToken))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_TARGET", "Phòng ban nhận không hợp lệ hoặc đã ngừng hoạt động.");
        if (input.ToUserId is { } targetUser)
        {
            if (!await transaction.UserExistsAndActiveAsync(targetUser, cancellationToken))
                throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_TARGET", "Người nhận không hợp lệ hoặc đã ngừng hoạt động.");
            var myLevel = await recipients.GetUserLevelAsync(tenantId, actorId, cancellationToken);
            var targetLevel = await recipients.GetUserLevelAsync(tenantId, targetUser, cancellationToken);
            if (targetLevel > myLevel + 1)
                throw new ApplicationFault(FaultKind.Forbidden, "REQUEST.LEVEL_SKIP", "Không được gửi vượt cấp. Chỉ gửi cho người ngang cấp hoặc cấp trên trực tiếp.");
        }

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        request.ApplyRoutingTransition(new(actorId, request.Status, RequestState.Routed, request.UpdatedAt), now);

        var previous = transaction.LatestRouting;
        var routing = RequestRouting.Create(tenantId, request.Id, actorId, now,
            input.ToDepartmentId, input.ToUserId, previous?.ToDepartmentId, previous?.ToUserId,
            input.Reason, RequestRoutingSource.Manual);
        var audit = AuditLog.RequestRouted(request, routing, now, null, null);

        var recipientIds = new List<Guid>();
        if (input.ToUserId is { } tu) recipientIds.Add(tu);
        else if (input.ToDepartmentId is { } td) recipientIds.AddRange(await transaction.GetDepartmentManagersAsync(td, cancellationToken));

        var notifications = recipientIds.Distinct()
            .Select(recipientId => Notification.Create(tenantId, recipientId, NotificationEvent.RequestRouted,
                "Yêu cầu mới", $"Bạn nhận được yêu cầu '{request.Title}'.",
                $"request-route/{routing.Id:N}", "Request", request.Id))
            .ToList();

        await transaction.CommitAsync(routing, audit, notifications, cancellationToken);
        if (recipientIds.Count > 0)
            await updates.PublishAsync(tenantId, recipientIds, cancellationToken);

        return new(request.Id, routing.Id, RequestListCodes.State(request.Status), routing.ToDepartmentId, routing.ToUserId, now);
    }
}
