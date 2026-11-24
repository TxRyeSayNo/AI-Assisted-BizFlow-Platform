using System.Net;
using System.Net.Sockets;
using BizFlow.Domain.Notifications;
using BizFlow.Infrastructure.Notifications;
using BizFlow.Infrastructure.Persistence;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class NotificationEmailOutboxTests(PostgresFixture database)
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now {get;set;}=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private static IConfigurationRoot Configuration(int port) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["ResetEmail:Enabled"]="true",["ResetEmail:Host"]="127.0.0.1",["ResetEmail:Port"]=port.ToString(),
        ["ResetEmail:AllowInsecureLoopback"]="true",["ResetEmail:FromAddress"]="notify@example.test",["ResetEmail:FrontendOrigin"]="https://bizflow.example.test"
    }).Build();
    private NotificationEmailOutboxDispatcher Dispatcher(IConfiguration configuration,TimeProvider clock)
    {
        var options=new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(database.ConnectionString).Options;
        return new(options,new EmailNotificationDispatcher(NullLogger<EmailNotificationDispatcher>.Instance,options,configuration),configuration,clock);
    }
    private async Task<Guid> Add(Guid tenant,Guid user,string key="event", bool rollback=false)
    {
        await using var db=database.Create(user,tenant);
        await using var transaction=await db.Database.BeginTransactionAsync();
        var notice=Notification.Create(tenant,user,NotificationEvent.ApprovalRequired,"Cần phê duyệt","Hồ sơ đã sẵn sàng kiểm tra.",key,"Request",Guid.NewGuid());
        db.Notifications.Add(notice);await db.SaveChangesAsync();
        if(rollback)await transaction.RollbackAsync();else await transaction.CommitAsync();
        return notice.Id;
    }

    [Fact]
    public async Task Reset_SMTP_configuration_delivers_committed_notifications_once_across_concurrent_workers()
    {
        var account=await database.SeedTenantAsync();var other=await database.SeedTenantAsync();
        var id=await Add(account.TenantId,account.UserId);var foreignId=await Add(other.TenantId,other.UserId);
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received=ResetEmailTests.ReceiveAsync(listener,timeout.Token);
        var configuration=Configuration(((IPEndPoint)listener.LocalEndpoint).Port);var clock=new Clock();
        var first=Dispatcher(configuration,clock);var second=Dispatcher(configuration,clock);
        var results=await Task.WhenAll(first.SweepTenantAsync(account.TenantId,timeout.Token),second.SweepTenantAsync(account.TenantId,timeout.Token));
        Assert.Equal(1,results.Sum());
        var message=await received;
        Assert.Equal("employee@example.test",Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("notify@example.test",Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal("BizFlow: Cần phê duyệt",message.Subject);
        Assert.Contains("Hồ sơ đã sẵn sàng kiểm tra.",message.TextBody);
        Assert.Contains("https://bizflow.example.test/requests/",message.TextBody);
        Assert.Equal($"notification-{id:N}@bizflow.local",message.MessageId);
        Assert.Equal(0,await first.SweepTenantAsync(account.TenantId,timeout.Token));
        await using var db=database.Create(account.UserId,account.TenantId);
        Assert.Equal("SENT",await db.Database.SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={id}").SingleAsync());
        Assert.Equal("PENDING",await db.Database.SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={foreignId}").SingleAsync());
    }

    [Fact]
    public async Task Queue_is_atomic_excludes_existing_deadline_pipeline_and_disabled_transport_does_not_consume_work()
    {
        var account=await database.SeedTenantAsync();
        var rolledBack=await Add(account.TenantId,account.UserId,"rolled-back",true);
        var reminder=await Add(account.TenantId,account.UserId,"reminder:test");
        var pending=await Add(account.TenantId,account.UserId,"ordinary");
        var configuration=Configuration(1);configuration["NotificationEmail:Enabled"]="false";
        Assert.Equal(0,await Dispatcher(configuration,new Clock()).SweepTenantAsync(account.TenantId,default));
        await using var db=database.Create(account.UserId,account.TenantId);
        var ids=await db.Database.SqlQuery<Guid>($"SELECT \"NotificationId\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"TenantId\"={account.TenantId}").ToArrayAsync();
        Assert.DoesNotContain(rolledBack,ids);Assert.DoesNotContain(reminder,ids);Assert.Equal(pending,Assert.Single(ids));
    }

    [Fact]
    public async Task Failed_SMTP_retries_with_backoff_and_stops_after_five_attempts()
    {
        var account=await database.SeedTenantAsync();var id=await Add(account.TenantId,account.UserId);
        // Reserve a closed local port: fail promptly without external SMTP traffic.
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var clock=new Clock();var dispatcher=Dispatcher(Configuration(port),clock);
        Assert.Equal(0,await dispatcher.SweepTenantAsync(account.TenantId,default));
        Assert.Equal(0,await dispatcher.SweepTenantAsync(account.TenantId,default));
        await using var db=database.Create(account.UserId,account.TenantId);
        Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT \"Attempts\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={id}").SingleAsync());
        for(var i=1;i<5;i++){clock.Now=clock.Now.AddHours(1);await dispatcher.SweepTenantAsync(account.TenantId,default);}
        Assert.Equal(5,await db.Database.SqlQuery<int>($"SELECT \"Attempts\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={id}").SingleAsync());
        Assert.Equal("FAILED",await db.Database.SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={id}").SingleAsync());
    }

    [Fact]
    public async Task Stale_notifications_and_inactive_recipients_are_skipped_without_SMTP()
    {
        var account=await database.SeedTenantAsync();var id=await Add(account.TenantId,account.UserId);
        var clock=new Clock{Now=DateTimeOffset.UtcNow.AddDays(2)};
        var configuration=Configuration(1);
        Assert.Equal(0,await Dispatcher(configuration,clock).SweepTenantAsync(account.TenantId,default));
        await using var db=database.Create(account.UserId,account.TenantId);
        Assert.Equal("SKIPPED",await db.Database.SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={id}").SingleAsync());
        var inactive=await Add(account.TenantId,account.UserId,"inactive");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={account.UserId}");
        Assert.Equal(0,await Dispatcher(configuration,new Clock()).SweepTenantAsync(account.TenantId,default));
        Assert.Equal("SKIPPED",await db.Database.SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"NotificationEmailDelivery\" WHERE \"NotificationId\"={inactive}").SingleAsync());
    }
}
