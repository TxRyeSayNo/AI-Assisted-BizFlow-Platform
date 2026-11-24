using System.Text;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Notifications;

public sealed record TaskDigestInput(DateOnly From,DateOnly To);
public sealed record TaskDigestResult(Guid NotificationId,int TaskCount);

public sealed class TaskDigestService(DbContextOptions<BizFlowDbContext> options,TimeProvider clock,INotificationUpdates updates,
    ILogger<TaskDigestService> logger)
{
    private sealed record DigestContext(Guid? TenantId,Guid? UserId):ITenantContext;
    public sealed class DigestRecipient {public Guid TenantId{get;set;} public Guid UserId{get;set;} public string TimeZone{get;set;}="UTC";public int DigestHour{get;set;} public int DigestDays{get;set;}}
    private static readonly TaskState[] OpenStates=[TaskState.Draft,TaskState.Assigned,TaskState.Accepted,TaskState.InProgress,TaskState.Submitted,TaskState.Overdue];

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        await using var db=new BizFlowDbContext(options,new DigestContext(null,null));
        var users=await db.Database.SqlQuery<DigestRecipient>($"""
            SELECT p."TenantId",p."UserId",t."TimeZone",p."DigestHour",p."DigestDays"
            FROM "NotificationPreference" p JOIN "Tenant" t ON t."TenantId"=p."TenantId"
            JOIN "User" u ON u."UserId"=p."UserId" AND u."TenantId"=p."TenantId"
            WHERE p."DailyDigestEnabled" AND t."Status"='ACTIVE' AND u."Status"='ACTIVE' AND u."DeletedAt" IS NULL
            ORDER BY p."TenantId",p."UserId"
            """).ToArrayAsync(ct);
        var count=0;
        foreach(var user in users)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var zone=TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);
                var local=TimeZoneInfo.ConvertTime(clock.GetUtcNow(),zone);
                if(local.Hour<user.DigestHour)continue;
                var day=DateOnly.FromDateTime(local.DateTime);
                var result=await CreateAsync(user.TenantId,user.UserId,new(day,day.AddDays(user.DigestDays-1)),true,ct);
                if(result is not null)count++;
            }
            catch(Exception error) when(error is not OperationCanceledException){logger.LogWarning("Daily task digest failed for user {UserId}.",user.UserId);}
        }
        return count;
    }

    public async Task<TaskDigestResult?> CreateAsync(Guid tenant,Guid user,TaskDigestInput input,bool daily,CancellationToken ct)
    {
        if(input.To<input.From || input.To.DayNumber-input.From.DayNumber>=31 || input.To==DateOnly.MaxValue)
            throw new ApplicationFault(FaultKind.Validation,"DIGEST.INVALID_RANGE","Chọn khoảng ngày hợp lệ, tối đa 31 ngày.");
        await using var db=new BizFlowDbContext(options,new DigestContext(tenant,user));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // The same user's manual and scheduled generations are serialized across API instances.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({$"digest:{tenant:N}:{user:N}"},0))",ct);
        if(!await db.Users.AnyAsync(u=>u.Id==user&&u.Status==UserStatus.Active,ct))throw ApplicationFault.NotFound();
        var workspace=await db.Tenants.SingleOrDefaultAsync(t=>t.Id==tenant&&t.Status==TenantStatus.Active,ct)??throw ApplicationFault.NotFound();
        var preference=await NotificationPreferenceStore.ReadAsync(db,tenant,user,ct);
        if(daily&&!preference.DailyDigestEnabled)return null;
        var key=daily?$"digest:daily:{user:N}:{input.From:yyyyMMdd}":$"digest:range:{user:N}:{input.From:yyyyMMdd}:{input.To:yyyyMMdd}";
        var existing=await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n=>n.IdempotencyKey==key,ct);
        if(existing is not null)return null;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone);
        var from=Boundary(input.From,zone);var until=Boundary(input.To.AddDays(1),zone);
        var query=db.WorkTasks.AsNoTracking().Where(t=>t.DeletedAt==null&&OpenStates.Contains(t.Status)&&t.Deadline>=from&&t.Deadline<until&&
            db.TaskAssignments.Any(a=>a.TaskId==t.Id&&a.UserId==user&&a.EndedAt==null));
        var total=await query.CountAsync(ct);
        var tasks=await query.OrderBy(t=>t.Deadline).ThenBy(t=>t.Id).Select(t=>new{t.Id,t.Title,t.Deadline}).Take(100).ToArrayAsync(ct);
        var content=new StringBuilder($"Khoảng hạn xử lý: {input.From:dd/MM/yyyy} – {input.To:dd/MM/yyyy} ({workspace.TimeZone}).\nCó {total} công việc chưa hoàn tất được giao trực tiếp cho bạn.\n");
        foreach(var task in tasks)
            content.AppendLine($"• {task.Title.Replace('\n',' ').Replace('\r',' ')} — {TimeZoneInfo.ConvertTime(task.Deadline!.Value,zone):dd/MM/yyyy HH:mm} — /tasks/{task.Id}");
        if(total>tasks.Length)content.AppendLine($"Hiển thị {tasks.Length}/{total} công việc. Mở danh sách công việc để xem đầy đủ.");
        if(total==0)content.AppendLine("Không có công việc đến hạn trong khoảng này.");
        var notice=Notification.Create(tenant,user,NotificationEvent.Announcement,daily?"Tổng hợp công việc đến hạn":"Tổng hợp công việc theo khoảng ngày",content.ToString(),key);
        db.Notifications.Add(notice);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        try{await updates.PublishAsync(tenant,[user],ct);}catch(Exception error) when(error is not OperationCanceledException){logger.LogWarning("Task digest stored but realtime update failed.");}
        return new(notice.Id,total);
    }

    private static DateTimeOffset Boundary(DateOnly date,TimeZoneInfo zone)
    {
        var local=date.ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified);
        // Some zones move clocks at midnight. Move to the first valid instant of that date.
        for(var i=0;zone.IsInvalidTime(local)&&i<1440;i++)local=local.AddMinutes(1);
        return new(TimeZoneInfo.ConvertTimeToUtc(local,zone),TimeSpan.Zero);
    }
}
