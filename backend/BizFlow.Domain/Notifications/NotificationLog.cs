using BizFlow.Domain.Common;

namespace BizFlow.Domain.Notifications;

public sealed class NotificationLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }
    public string TriggerType { get; private set; } = "";
    public DateTimeOffset ScheduledAt { get; private set; }
    public string Channel { get; private set; } = ""; // "InApp", "WebPush", "Email"
    public string Status { get; private set; } = ""; // "Sent", "Failed", "Skipped"
    public DateTimeOffset DeliveredAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    private NotificationLog() { }

    public static NotificationLog Create(
        Guid tenantId,
        Guid taskId,
        Guid userId,
        string triggerType,
        DateTimeOffset scheduledAt,
        string channel,
        string status,
        DateTimeOffset deliveredAt,
        string? errorMessage = null)
    {
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            TaskId = EntityRules.Id(taskId, nameof(taskId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            TriggerType = EntityRules.Text(triggerType, 50, nameof(triggerType)),
            ScheduledAt = scheduledAt.ToUniversalTime(),
            Channel = EntityRules.Text(channel, 30, nameof(channel)),
            Status = EntityRules.Text(status, 30, nameof(status)),
            DeliveredAt = deliveredAt.ToUniversalTime(),
            ErrorMessage = errorMessage is null ? null : EntityRules.Text(errorMessage, 1000, nameof(errorMessage))
        };
    }
}
