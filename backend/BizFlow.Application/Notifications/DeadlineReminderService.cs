using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Notifications;
namespace BizFlow.Application.Notifications;

public sealed class DeadlineReminderService(
    ITenantContext context,
    TenantMembershipAuthorizer membership,
    IDeadlineReminderStore store,
    TimeProvider clock,
    IVapidKeyProvider vapidKeyProvider) : IDeadlineReminderService
{
    public async Task<IReadOnlyList<DeadlineReminderDto>> ConfigureRemindersAsync(
        ConfigureReminderInput input,
        CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var userId = context.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active user account required.");

        if (input.TaskId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "TaskId is required.");

        var task = await store.GetTaskAsync(tenantId, input.TaskId, cancellationToken);
        if (task is null)
            throw ApplicationFault.NotFound();

        if (!task.Deadline.HasValue)
            throw new ApplicationFault(FaultKind.Validation, "REMINDER.NO_DEADLINE", "Cannot configure deadline reminders for a task without a deadline.");

        var now = clock.GetUtcNow();
        var leadTimes = input.LeadTimeMinutes.Count > 0 ? input.LeadTimeMinutes : [1440, 240, 60, 0];
        var createdList = new List<DeadlineReminderDto>();

        foreach (var minutes in leadTimes)
        {
            var triggerType = minutes switch
            {
                >= 1440 => "24h",
                >= 240 => "4h",
                >= 60 => "1h",
                _ => "Overdue"
            };

            var schedule = DeadlineReminderSchedule.Create(
                tenantId,
                task.Id,
                userId,
                minutes,
                task.Deadline.Value,
                triggerType,
                now);

            await store.SaveScheduleAsync(schedule, cancellationToken);

            createdList.Add(new DeadlineReminderDto(
                schedule.Id,
                schedule.TaskId,
                schedule.UserId,
                schedule.LeadTimeMinutes,
                schedule.RemindAt,
                schedule.TriggerType,
                schedule.Status,
                schedule.CreatedAt));
        }

        await store.SaveChangesAsync(cancellationToken);
        return createdList;
    }

    public async Task<IReadOnlyList<DeadlineReminderDto>> GetRemindersForTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var schedules = await store.GetSchedulesForTaskAsync(tenantId, taskId, cancellationToken);

        return schedules.Select(s => new DeadlineReminderDto(
            s.Id,
            s.TaskId,
            s.UserId,
            s.LeadTimeMinutes,
            s.RemindAt,
            s.TriggerType,
            s.Status,
            s.CreatedAt)).ToArray();
    }

    public async Task CancelReminderAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var schedule = await store.GetScheduleByIdAsync(tenantId, reminderId, cancellationToken);
        if (schedule is null)
            throw ApplicationFault.NotFound();

        schedule.Cancel();
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<PushSubscriptionResult> RegisterPushSubscriptionAsync(
        RegisterPushSubscriptionInput input,
        CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var userId = context.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active user account required.");

        if (string.IsNullOrWhiteSpace(input.Endpoint))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Endpoint is required.");

        var existing = await store.GetPushSubscriptionAsync(tenantId, userId, input.Endpoint, cancellationToken);
        var now = clock.GetUtcNow();

        if (existing is not null)
        {
            existing.UpdateActivity(now);
        }
        else
        {
            var subscription = WebPushSubscription.Create(
                tenantId,
                userId,
                input.Endpoint,
                input.P256dh,
                input.Auth,
                now);

            await store.AddPushSubscriptionAsync(subscription, cancellationToken);
        }

        await store.SaveChangesAsync(cancellationToken);
        return new PushSubscriptionResult(true, "WebPush subscription registered successfully.");
    }

    public async Task<PushSubscriptionResult> UnregisterPushSubscriptionAsync(
        UnregisterPushSubscriptionInput input,
        CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var userId = context.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active user account required.");

        var existing = await store.GetPushSubscriptionAsync(tenantId, userId, input.Endpoint, cancellationToken);
        if (existing is not null)
        {
            await store.RemovePushSubscriptionAsync(existing, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
        }

        return new PushSubscriptionResult(true, "WebPush subscription removed successfully.");
    }

    public string GetVapidPublicKey()
    {
        return vapidKeyProvider.GetVapidPublicKey();
    }
}
