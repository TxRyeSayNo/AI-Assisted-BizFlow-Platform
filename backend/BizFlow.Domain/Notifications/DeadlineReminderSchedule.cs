using BizFlow.Domain.Common;

namespace BizFlow.Domain.Notifications;

public sealed class DeadlineReminderSchedule
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }
    public int LeadTimeMinutes { get; private set; }
    public DateTimeOffset RemindAt { get; private set; }
    public string TriggerType { get; private set; } = "";
    public string Status { get; private set; } = "Scheduled";
    public DateTimeOffset CreatedAt { get; private set; }

    private DeadlineReminderSchedule() { }

    public static DeadlineReminderSchedule Create(
        Guid tenantId,
        Guid taskId,
        Guid userId,
        int leadTimeMinutes,
        DateTimeOffset taskDeadline,
        string triggerType,
        DateTimeOffset now)
    {
        var remindAt = triggerType.Equals("Overdue", StringComparison.OrdinalIgnoreCase)
            ? taskDeadline
            : taskDeadline.AddMinutes(-leadTimeMinutes);

        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            TaskId = EntityRules.Id(taskId, nameof(taskId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            LeadTimeMinutes = leadTimeMinutes,
            RemindAt = remindAt,
            TriggerType = EntityRules.Text(triggerType, 50, nameof(triggerType)),
            Status = "Scheduled",
            CreatedAt = now.ToUniversalTime()
        };
    }

    public void MarkSent() => Status = "Sent";
    public void Cancel() => Status = "Cancelled";
}
