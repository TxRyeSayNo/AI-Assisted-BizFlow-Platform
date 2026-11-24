using BizFlow.Application.Notifications;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Notifications;

public sealed record DeadlineSweepResult(int SchedulesCreated, int RemindersSent);

public interface IDeadlineReminderDispatcher
{
    Task<DeadlineSweepResult> SweepAsync(CancellationToken cancellationToken);

    /// <summary>Sweeps one tenant. Separated so it can be driven directly by a test.</summary>
    Task<DeadlineSweepResult> SweepTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Completes GAP-01: the background half of the multi-channel deadline reminder module.
///
/// Two phases per tenant. First it provisions the missing <see cref="DeadlineReminderSchedule"/>
/// rows for dated, still-open, assigned tasks at the documented 24h / 4h / 1h / overdue marks.
/// Then it dispatches whatever is now due, escalating in-app → web push → email: a person who is
/// connected gets the realtime nudge and nothing more, and the noisier channels are used only
/// while the quieter one could not reach them.
///
/// Idempotency: only rows still in <c>Scheduled</c> are picked up, and each is flipped to
/// <c>Sent</c> in the same commit as its <see cref="NotificationLog"/> entries, so a second sweep
/// — or a restart halfway through — never sends the same reminder twice.
///
/// Tenancy: the only cross-tenant read is the list of tenant ids, which no per-tenant filter can
/// express. Everything else runs through a context pinned to a single tenant with the fail-closed
/// query filters in force; nothing here calls IgnoreQueryFilters.
/// </summary>
public sealed class DeadlineReminderDispatcher(
    DbContextOptions<BizFlowDbContext> options,
    INotificationUpdates updates,
    IRealtimePresenceChecker presence,
    IWebPushDispatcher webPush,
    IEmailNotificationDispatcher email,
    TimeProvider clock,
    ILogger<DeadlineReminderDispatcher> logger) : IDeadlineReminderDispatcher
{
    /// <summary>
    /// The documented reminder marks. The trigger names match what
    /// <see cref="IDeadlineReminderService"/> already writes, because the existing table carries a
    /// unique index on (TenantId, TaskId, UserId, TriggerType): one row per mark, never per
    /// lead time, so the vocabulary has to be shared rather than reinvented.
    /// </summary>
    public static readonly (int Minutes, string Trigger)[] Marks =
        [(24 * 60, "24h"), (4 * 60, "4h"), (60, "1h")];

    public const string OverdueTrigger = "Overdue";

    /// <summary>Lead times only, for callers that just need the minute values.</summary>
    public static int[] LeadTimesMinutes => [.. Marks.Select(m => m.Minutes)];

    /// <summary>A reminder older than this is stale: log it as skipped instead of shouting late.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(3);

    /// <summary>Upper bound per tenant per sweep, so one busy tenant cannot starve the others.</summary>
    public const int MaxPerTenantPerSweep = 200;

    private static readonly TaskState[] OpenStates =
        [TaskState.Draft, TaskState.Assigned, TaskState.Accepted, TaskState.InProgress,
         TaskState.Submitted, TaskState.Overdue];

    public async Task<DeadlineSweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        var created = 0; var sent = 0;
        foreach (var tenantId in await ActiveTenantIdsAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var result = await SweepTenantAsync(tenantId, cancellationToken);
                created += result.SchedulesCreated;
                sent += result.RemindersSent;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogWarning(failure, "Deadline reminder sweep failed for tenant {TenantId}.", tenantId);
            }
        }
        return new(created, sent);
    }

    public async Task<DeadlineSweepResult> SweepTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var created = await ProvisionAsync(tenantId, now, cancellationToken);
        var sent = await DispatchAsync(tenantId, now, cancellationToken);
        if (created > 0 || sent > 0)
            logger.LogInformation("Deadline sweep for tenant {TenantId}: {Created} scheduled, {Sent} sent.",
                tenantId, created, sent);
        return new(created, sent);
    }

    /// <summary>Creates the missing schedule rows for dated, open, assigned tasks.</summary>
    private async Task<int> ProvisionAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = new BizFlowDbContext(options, new JobContext(tenantId));
        var candidates = await db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Deadline != null && OpenStates.Contains(t.Status))
            .Select(t => new
            {
                t.Id,
                Deadline = t.Deadline!.Value,
                AssigneeId = db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId != null)
                    .Select(a => a.UserId).FirstOrDefault()
            })
            .Where(t => t.AssigneeId != null)
            .Take(MaxPerTenantPerSweep)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0) return 0;

        var taskIds = candidates.Select(c => c.Id).ToArray();
        var existing = await db.DeadlineReminderSchedules.AsNoTracking()
            .Where(s => s.TenantId == tenantId && taskIds.Contains(s.TaskId))
            .Select(s => new { s.TaskId, s.UserId, s.TriggerType })
            .ToListAsync(cancellationToken);
        // Matches the unique index exactly, so the sweep never collides with a mark a person
        // configured for themselves through the REST endpoints.
        var seen = existing.Select(e => (e.TaskId, e.UserId, e.TriggerType)).ToHashSet();

        var fresh = new List<DeadlineReminderSchedule>();
        foreach (var candidate in candidates)
        {
            var userId = candidate.AssigneeId!.Value;
            foreach (var (minutes, trigger) in Marks)
            {
                if (seen.Contains((candidate.Id, userId, trigger))) continue;
                // Never schedule a warning whose moment has already gone by.
                if (candidate.Deadline.AddMinutes(-minutes) <= now) continue;
                fresh.Add(DeadlineReminderSchedule.Create(tenantId, candidate.Id, userId, minutes,
                    candidate.Deadline, trigger, now));
            }
            if (!seen.Contains((candidate.Id, userId, OverdueTrigger)))
                fresh.Add(DeadlineReminderSchedule.Create(tenantId, candidate.Id, userId, 0,
                    candidate.Deadline, OverdueTrigger, now));
        }
        if (fresh.Count == 0) return 0;
        db.DeadlineReminderSchedules.AddRange(fresh);
        await db.SaveChangesAsync(cancellationToken);
        return fresh.Count;
    }

    /// <summary>Sends everything that is due, one schedule row at a time.</summary>
    private async Task<int> DispatchAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = new BizFlowDbContext(options, new JobContext(tenantId));
        var due = await db.DeadlineReminderSchedules
            .Where(s => s.TenantId == tenantId && s.Status == "Scheduled" && s.RemindAt <= now)
            .OrderBy(s => s.RemindAt)
            .Take(MaxPerTenantPerSweep)
            .ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;

        var taskIds = due.Select(s => s.TaskId).Distinct().ToArray();
        var tasks = await db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && taskIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title, t.Deadline, t.Status })
            .ToListAsync(cancellationToken);

        var logs = new List<NotificationLog>();
        var inApp = new List<Notification>();
        var recipients = new HashSet<Guid>();
        var sent = 0;
        foreach (var schedule in due)
        {
            var task = tasks.SingleOrDefault(t => t.Id == schedule.TaskId);
            // The task vanished, closed, or lost its deadline: retire the reminder quietly.
            if (task is null || !OpenStates.Contains(task.Status) || task.Deadline is null)
            {
                schedule.Cancel();
                logs.Add(Log(tenantId, schedule, "InApp", "Skipped", now, "Task is no longer awaiting this deadline."));
                continue;
            }
            if (now - schedule.RemindAt > StaleAfter)
            {
                schedule.MarkSent();
                logs.Add(Log(tenantId, schedule, "InApp", "Skipped", now, "Reminder was already stale when the sweep ran."));
                continue;
            }

            var overdue = schedule.TriggerType.Equals(OverdueTrigger, StringComparison.OrdinalIgnoreCase);
            var title = overdue ? "Nhiệm vụ đã quá hạn" : "Nhiệm vụ sắp đến hạn";
            var body = overdue
                ? $"\"{task.Title}\" đã quá hạn lúc {task.Deadline:dd/MM/yyyy HH:mm}."
                : $"\"{task.Title}\" đến hạn lúc {task.Deadline:dd/MM/yyyy HH:mm}.";

            // The inbox row is the durable record and is always written.
            inApp.Add(Notification.Create(tenantId, schedule.UserId,
                overdue ? NotificationEvent.Overdue : NotificationEvent.DeadlineWarning,
                title, body, $"reminder:{schedule.Id}", "TASK", schedule.TaskId));
            recipients.Add(schedule.UserId);

            // The log carries exactly one row per reminder — its unique index is
            // (TenantId, TaskId, UserId, TriggerType, ScheduledAt) and deliberately excludes the
            // channel — so it records the channel that actually reached the person, with the
            // escalation it went through in the message.
            string channel = "InApp", status = "Sent";
            string? detail = null;
            var preference=await NotificationPreferenceStore.ReadAsync(db,tenantId,schedule.UserId,cancellationToken);
            var online=SafePresence(tenantId,schedule.UserId);
            if (preference.EmailEnabled && preference.DeadlineEmailMode=="Always")
            {
                var (delivered,error)=await TryEmailAsync(tenantId,schedule,task.Title,task.Deadline,cancellationToken);
                channel="Email";status=delivered?"Sent":"Failed";detail=error;
            }
            else if (!online)
            {
                var push = await TryPushAsync(tenantId, schedule, title, body, cancellationToken);
                if (push.Delivered)
                {
                    channel = "WebPush";
                }
                else if (preference.EmailEnabled && preference.DeadlineEmailMode=="AwayOnly")
                {
                    var (delivered, error) = await TryEmailAsync(tenantId, schedule, task.Title, task.Deadline, cancellationToken);
                    channel = "Email";
                    status = delivered ? "Sent" : "Failed";
                    detail = Trim($"web push unavailable: {push.Error ?? "not delivered"}" +
                        (delivered ? "" : $"; email failed: {error}"));
                }
            }
            logs.Add(Log(tenantId, schedule, channel, status, now, detail));
            schedule.MarkSent();
            sent++;
        }

        db.Notifications.AddRange(inApp);
        db.NotificationLogs.AddRange(logs);
        // One commit flips every schedule and stores its log, which is what makes a replay safe.
        await db.SaveChangesAsync(cancellationToken);

        if (recipients.Count > 0)
        {
            try
            {
                await updates.PublishAsync(tenantId, [.. recipients], cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogWarning(failure, "Realtime publish failed after a deadline sweep for tenant {TenantId}.", tenantId);
            }
        }
        return sent;
    }

    private static NotificationLog Log(Guid tenantId, DeadlineReminderSchedule schedule, string channel,
        string status, DateTimeOffset now, string? detail) =>
        NotificationLog.Create(tenantId, schedule.TaskId, schedule.UserId, schedule.TriggerType,
            schedule.RemindAt, channel, status, now, detail);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= 1000 ? value : value[..1000];

    private bool SafePresence(Guid tenantId, Guid userId)
    {
        try { return presence.IsUserOnline(tenantId, userId); }
        catch (Exception failure)
        {
            // Unknown presence means escalate, never swallow a reminder.
            logger.LogDebug(failure, "Presence lookup failed; treating the recipient as offline.");
            return false;
        }
    }

    private async Task<WebPushDispatchResult> TryPushAsync(Guid tenantId, DeadlineReminderSchedule schedule,
        string title, string body, CancellationToken cancellationToken)
    {
        try
        {
            return await webPush.SendNotificationAsync(tenantId, schedule.UserId, title, body,
                $"/tasks/{schedule.TaskId}", cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "Web push failed for reminder {ReminderId}.", schedule.Id);
            return new(false, false, "Web push transport failed.");
        }
    }

    private async Task<(bool Delivered, string? Error)> TryEmailAsync(Guid tenantId, DeadlineReminderSchedule schedule,
        string taskTitle, DateTimeOffset? deadline, CancellationToken cancellationToken)
    {
        try
        {
            var delivered = await email.SendDeadlineEmailAsync(tenantId, schedule.UserId, taskTitle, deadline,
                schedule.TriggerType, cancellationToken);
            return (delivered, delivered ? null : "Email provider reported a failure.");
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "Deadline email failed for reminder {ReminderId}.", schedule.Id);
            return (false, "Email transport failed.");
        }
    }

    private async Task<IReadOnlyList<Guid>> ActiveTenantIdsAsync(CancellationToken cancellationToken)
    {
        await using var db = new BizFlowDbContext(options, new JobContext(null));
        return await db.Database
            .SqlQuery<Guid>($"SELECT \"TenantId\" AS \"Value\" FROM \"Tenant\" WHERE \"Status\" = 'ACTIVE'")
            .ToListAsync(cancellationToken);
    }

    private sealed class JobContext(Guid? tenantId) : BizFlow.Application.Security.ITenantContext
    {
        public Guid? UserId { get; } = tenantId is null ? null : SystemActorId;
        public Guid? TenantId { get; } = tenantId;
        internal static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");
    }
}
