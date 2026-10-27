using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Notifications;

public sealed class DeadlineReminderStore(BizFlowDbContext db) : IDeadlineReminderStore
{
    public Task<WorkTask?> GetTaskAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
    {
        return db.WorkTasks
            .SingleOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId && t.DeletedAt == null, cancellationToken);
    }

    public async Task<IReadOnlyList<DeadlineReminderSchedule>> GetSchedulesForTaskAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        return await db.DeadlineReminderSchedules
            .Where(s => s.TaskId == taskId && s.TenantId == tenantId)
            .OrderBy(s => s.RemindAt)
            .ToListAsync(cancellationToken);
    }

    public Task<DeadlineReminderSchedule?> GetScheduleByIdAsync(
        Guid tenantId,
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        return db.DeadlineReminderSchedules
            .SingleOrDefaultAsync(s => s.Id == reminderId && s.TenantId == tenantId, cancellationToken);
    }

    public async Task SaveScheduleAsync(DeadlineReminderSchedule schedule, CancellationToken cancellationToken)
    {
        var existing = await db.DeadlineReminderSchedules
            .SingleOrDefaultAsync(s => s.TenantId == schedule.TenantId &&
                                       s.TaskId == schedule.TaskId &&
                                       s.UserId == schedule.UserId &&
                                       s.TriggerType == schedule.TriggerType, cancellationToken);

        if (existing is not null)
        {
            db.DeadlineReminderSchedules.Remove(existing);
        }

        db.DeadlineReminderSchedules.Add(schedule);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }

    public Task<WebPushSubscription?> GetPushSubscriptionAsync(
        Guid tenantId,
        Guid userId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        return db.WebPushSubscriptions
            .SingleOrDefaultAsync(s => s.TenantId == tenantId && s.UserId == userId && s.Endpoint == endpoint, cancellationToken);
    }

    public Task AddPushSubscriptionAsync(WebPushSubscription subscription, CancellationToken cancellationToken)
    {
        db.WebPushSubscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public Task RemovePushSubscriptionAsync(WebPushSubscription subscription, CancellationToken cancellationToken)
    {
        db.WebPushSubscriptions.Remove(subscription);
        return Task.CompletedTask;
    }
}

public sealed class ConfigVapidKeyProvider(Microsoft.Extensions.Configuration.IConfiguration configuration) : IVapidKeyProvider
{
    public string GetVapidPublicKey() =>
        configuration["WebPush:VapidPublicKey"] ?? "BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBKr3qBUYIHBQFLXYp5Nksh8U";
}

