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

    /// <summary>
    /// Re-arms this mark in place, keeping its identity, owner and creation time. Reconfiguring a
    /// reminder has to reuse the existing row: the table allows one row per
    /// (tenant, task, user, trigger) and the platform forbids hard-deleting history.
    /// </summary>
    public void Reschedule(int leadTimeMinutes, DateTimeOffset remindAt)
    {
        LeadTimeMinutes = leadTimeMinutes;
        RemindAt = remindAt;
        Status = "Scheduled";
    }
}
