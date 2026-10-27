namespace BizFlow.Application.Notifications;

public sealed record RegisterPushSubscriptionInput(string Endpoint, string P256dh, string Auth);
public sealed record UnregisterPushSubscriptionInput(string Endpoint);

public sealed record ConfigureReminderInput(
    Guid TaskId,
    IReadOnlyList<int> LeadTimeMinutes,
    IReadOnlyList<string>? TriggerTypes = null);

public sealed record DeadlineReminderDto(
    Guid Id,
    Guid TaskId,
    Guid UserId,
    int LeadTimeMinutes,
    DateTimeOffset RemindAt,
    string TriggerType,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record PushSubscriptionResult(bool Success, string Message);

public sealed record WebPushDispatchResult(bool Delivered, bool SubscriptionExpired, string? Error = null);

public interface IVapidKeyProvider
{
    string GetVapidPublicKey();
}

public interface IDeadlineReminderService
{
    Task<IReadOnlyList<DeadlineReminderDto>> ConfigureRemindersAsync(ConfigureReminderInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeadlineReminderDto>> GetRemindersForTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task CancelReminderAsync(Guid reminderId, CancellationToken cancellationToken);
    Task<PushSubscriptionResult> RegisterPushSubscriptionAsync(RegisterPushSubscriptionInput input, CancellationToken cancellationToken);
    Task<PushSubscriptionResult> UnregisterPushSubscriptionAsync(UnregisterPushSubscriptionInput input, CancellationToken cancellationToken);
    string GetVapidPublicKey();
}

public interface IDeadlineReminderStore
{
    Task<Domain.Tasks.WorkTask?> GetTaskAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Domain.Notifications.DeadlineReminderSchedule>> GetSchedulesForTaskAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken);
    Task<Domain.Notifications.DeadlineReminderSchedule?> GetScheduleByIdAsync(Guid tenantId, Guid reminderId, CancellationToken cancellationToken);
    Task SaveScheduleAsync(Domain.Notifications.DeadlineReminderSchedule schedule, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<Domain.Notifications.WebPushSubscription?> GetPushSubscriptionAsync(Guid tenantId, Guid userId, string endpoint, CancellationToken cancellationToken);
    Task AddPushSubscriptionAsync(Domain.Notifications.WebPushSubscription subscription, CancellationToken cancellationToken);
    Task RemovePushSubscriptionAsync(Domain.Notifications.WebPushSubscription subscription, CancellationToken cancellationToken);
}

public interface IRealtimePresenceChecker
{
    bool IsUserOnline(Guid tenantId, Guid userId);
}

public interface IWebPushDispatcher
{
    Task<WebPushDispatchResult> SendNotificationAsync(Guid tenantId, Guid userId, string title, string message, string? targetUrl, CancellationToken cancellationToken);
}

public interface IEmailNotificationDispatcher
{
    Task<bool> SendDeadlineEmailAsync(Guid tenantId, Guid userId, string taskTitle, DateTimeOffset? deadline, string triggerType, CancellationToken cancellationToken);
}
