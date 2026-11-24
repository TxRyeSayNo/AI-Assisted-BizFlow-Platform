using BizFlow.Domain.Common;
using BizFlow.Domain.Sla;
using System.Text.Json;

namespace BizFlow.Domain.Workflows;

public sealed class WorkflowSlaPause
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public Guid StepExecutionId { get; private set; }
    public Guid PausedBy { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset PausedAt { get; private set; }
    public DateTimeOffset DueAtBefore { get; private set; }
    public string? CalendarSnapshotJson { get; private set; }
    public DateTimeOffset? ResumedAt { get; private set; }
    public Guid? ResumedBy { get; private set; }
    public DateTimeOffset? DueAtAfter { get; private set; }
    private WorkflowSlaPause() { }
    public static WorkflowSlaPause Begin(WorkflowStepExecution step, Guid actor, string reason, DateTimeOffset now, BusinessCalendar? calendar)
    {
        reason = EntityRules.Text(reason, 1000, nameof(reason));
        _ = EntityRules.Id(actor, nameof(actor));
        step.Pause(now);
        return new() { Id = Guid.CreateVersion7(), TenantId = step.TenantId, InstanceId = step.InstanceId, StepExecutionId = step.Id,
            PausedBy = actor, Reason = reason, PausedAt = now.ToUniversalTime(),
            DueAtBefore = step.DueAt!.Value, CalendarSnapshotJson = calendar is null ? null : JsonSerializer.Serialize(new {
                calendar.TimeZone, calendar.WorkingHoursJson, calendar.HolidaysJson }) };
    }
    public void Resume(WorkflowStepExecution step, Guid actor, DateTimeOffset now)
    {
        _ = EntityRules.Id(actor, nameof(actor));
        if (ResumedAt is not null || step.Id != StepExecutionId || step.PausedAt != PausedAt || now < PausedAt)
            throw new InvalidOperationException("Khoảng tạm dừng đã thay đổi.");
        var dueAt = DueAtBefore.Add(now - PausedAt);
        if (CalendarSnapshotJson is not null)
        {
            using var snapshot = JsonDocument.Parse(CalendarSnapshotJson);
            var s = snapshot.RootElement;
            var calendar = BusinessCalendar.Create(TenantId, s.GetProperty("TimeZone").GetString()!,
                s.GetProperty("WorkingHoursJson").GetString()!, s.GetProperty("HolidaysJson").GetString()!);
            dueAt = BusinessTimeCalculator.AddWorkingMinutes(calendar, DueAtBefore,
                BusinessTimeCalculator.WorkingMinutesBetween(calendar, PausedAt, now));
        }
        step.Resume(now, dueAt);
        ResumedAt = now.ToUniversalTime(); ResumedBy = EntityRules.Id(actor, nameof(actor)); DueAtAfter = dueAt;
    }
}
