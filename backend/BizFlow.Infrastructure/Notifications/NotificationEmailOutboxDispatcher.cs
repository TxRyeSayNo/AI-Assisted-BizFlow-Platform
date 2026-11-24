using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Notifications;

public sealed class NotificationEmailOutboxDispatcher(DbContextOptions<BizFlowDbContext> options,
    EmailNotificationDispatcher sender, IConfiguration configuration, TimeProvider clock,
    BizFlow.Application.Authentication.IPasswordResetTokenProvider? tokens=null,
    BizFlow.Application.Authentication.PasswordResetPolicy? passwordPolicy=null,
    BizFlow.Application.Notifications.IRealtimePresenceChecker? presence=null)
{
    private sealed record DeliveryContext(Guid? TenantId, Guid? UserId) : ITenantContext;
    public sealed class PendingDelivery
    {
        public Guid NotificationId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int Attempts { get; set; }
    }

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var settings = NotificationEmailConfiguration.Resolve(configuration);
        if (!settings.Enabled || !settings.IsValid) return 0;
        await using var scan = new BizFlowDbContext(options, new DeliveryContext(null, null));
        var tenants = await scan.Database.SqlQuery<Guid>($"SELECT \"TenantId\" AS \"Value\" FROM \"Tenant\" WHERE \"Status\"='ACTIVE'").ToArrayAsync(ct);
        var sent = 0;
        foreach (var tenant in tenants) sent += await SweepTenantAsync(tenant, ct);
        return sent;
    }

    public async Task<int> SweepTenantAsync(Guid tenant, CancellationToken ct)
    {
        var settings = NotificationEmailConfiguration.Resolve(configuration);
        if (!settings.Enabled || !settings.IsValid) return 0;
        var sent = 0;
        for (var i = 0; i < 20; i++)
        {
            ct.ThrowIfCancellationRequested();
            var now = clock.GetUtcNow();
            await using var db = new BizFlowDbContext(options, new DeliveryContext(tenant, new Guid("00000000-0000-0000-0000-000000000001")));
            if (!await db.Tenants.AnyAsync(t => t.Status == BizFlow.Domain.Tenancy.TenantStatus.Active, ct)) break;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // The row lock spans delivery, so two API instances cannot simultaneously send the same entry.
            var pending = await db.Database.SqlQuery<PendingDelivery>($"""
                SELECT "NotificationId","CreatedAt","Attempts" FROM "NotificationEmailDelivery"
                WHERE "TenantId"={tenant} AND "Status"='PENDING' AND "NextAttemptAt"<={now}
                ORDER BY "NextAttemptAt","NotificationId" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).ToArrayAsync(ct);
            if (pending.Length == 0) break;
            var item = pending[0];
            var notice = await db.Notifications.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(n => n.Id == item.NotificationId && n.TenantId == tenant, ct);
            var recipient=notice is null?null:await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==notice.RecipientId&&u.Status==UserStatus.Active,ct);
            var invitation=notice?.IdempotencyKey.StartsWith("account-invitation:",StringComparison.Ordinal)==true;
            var preference=recipient is null?new NotificationPreferencesView():await NotificationPreferenceStore.ReadAsync(db,tenant,recipient.Id,ct);
            var deadline=notice?.Type is "DeadlineWarning" or "Overdue" or "Escalation";
            var online=false;
            try{online=recipient is not null&&presence?.IsUserOnline(tenant,recipient.Id)==true;}catch{ /* Unknown presence means away. */ }
            var available = recipient is not null && (invitation || preference.EmailEnabled &&
                (!deadline || preference.DeadlineEmailMode=="Always" || preference.DeadlineEmailMode=="AwayOnly"&&!online) &&
                (notice?.IdempotencyKey.StartsWith("digest:",StringComparison.Ordinal)!=true || preference.DigestEmail));
            var status = "SKIPPED";
            var attempts = item.Attempts;
            DateTimeOffset? deliveredAt = null;
            var next = now;
            // Do not replay stale alerts when SMTP is enabled after a long period offline.
            if (available && (invitation || now - item.CreatedAt <= TimeSpan.FromDays(1)))
            {
                attempts++;
                var path = notice!.ObjectType?.ToUpperInvariant() switch {
                    "TASK" when notice.ObjectId.HasValue => $"/tasks/{notice.ObjectId}",
                    "REQUEST" when notice.ObjectId.HasValue => $"/requests/{notice.ObjectId}",
                    _ => "/notifications"
                };
                var content=notice.Content;
                if(invitation && recipient!.MustChangePassword)
                {
                    if(tokens is null)throw new InvalidOperationException("Password invitation provider is unavailable.");
                    var tenantKey=await db.Tenants.Where(t=>t.Id==tenant).Select(t=>t.TenantKey).SingleAsync(ct);
                    var policy=passwordPolicy??new();
                    var token=tokens.Issue(new(recipient.Id,tenant,recipient.SecurityStamp,recipient.NormalizedEmail,now.AddMinutes(policy.TokenLifetimeMinutes)),now);
                    path="/reset-password#token="+Uri.EscapeDataString(token)+"&tenantKey="+Uri.EscapeDataString(tenantKey)+"&identifier="+Uri.EscapeDataString(recipient.EmployeeCode);
                    content+=$"\nLiên kết đặt mật khẩu có hạn {policy.TokenLifetimeMinutes} phút, dùng một lần. Nếu hết hạn, dùng Quên mật khẩu. Không chia sẻ liên kết này.";
                }
                var delivered = await sender.SendAsync(tenant, notice.RecipientId, notice.Title, content, path, notice.Id, ct);
                status = delivered ? "SENT" : attempts >= 5 ? "FAILED" : "PENDING";
                if (delivered) { deliveredAt = now; sent++; }
                next = now.AddMinutes(attempts switch { 1 => 1, 2 => 5, 3 => 15, _ => 60 });
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "NotificationEmailDelivery" SET "Status"={status},"Attempts"={attempts},"NextAttemptAt"={next},
                  "LastAttemptAt"={now},"SentAt"={deliveredAt}
                WHERE "NotificationId"={item.NotificationId} AND "TenantId"={tenant} AND "Status"='PENDING'
                """, ct);
            await transaction.CommitAsync(ct);
        }
        return sent;
    }
}

public sealed class NotificationEmailWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<NotificationEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = NotificationEmailConfiguration.Resolve(configuration);
        if (!settings.Enabled || !settings.IsValid) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<BizFlow.Infrastructure.Tenancy.CompanyRegistrationEmailDispatcher>().SweepAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<NotificationEmailOutboxDispatcher>().SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch { logger.LogWarning("Notification email queue could not be processed; it will be retried."); }
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
