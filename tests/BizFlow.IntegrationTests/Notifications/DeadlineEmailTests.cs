using System.Net;
using System.Net.Sockets;
using BizFlow.Infrastructure.Notifications;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using BizFlow.Infrastructure.Persistence;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class DeadlineEmailTests(PostgresFixture database)
{
    [Fact]
    public async Task Configured_transport_delivers_real_SMTP_message_and_unconfigured_transport_does_not_claim_success()
    {
        var account = await database.SeedTenantAsync();
        var foreign = await database.SeedTenantAsync();
        var options = new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(database.ConnectionString).Options;
        var disabled = new EmailNotificationDispatcher(NullLogger<EmailNotificationDispatcher>.Instance,options,new ConfigurationBuilder().Build());
        Assert.False(await disabled.SendDeadlineEmailAsync(account.TenantId,account.UserId,"Công việc",DateTimeOffset.UtcNow,"24h",default));
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var receive = ResetEmailTests.ReceiveAsync(listener,timeout.Token);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["NotificationEmail:Enabled"]="true",["NotificationEmail:Host"]="127.0.0.1",["NotificationEmail:Port"]=((IPEndPoint)listener.LocalEndpoint).Port.ToString(),
            ["NotificationEmail:AllowInsecureLoopback"]="true",["NotificationEmail:FromAddress"]="notify@example.test",["NotificationEmail:FrontendOrigin"]="https://bizflow.example.test"
        }).Build();
        var sender = new EmailNotificationDispatcher(NullLogger<EmailNotificationDispatcher>.Instance,options,configuration);
        Assert.False(await sender.SendDeadlineEmailAsync(account.TenantId,foreign.UserId,"Không được gửi khác tenant",null,"4h",timeout.Token));
        Assert.True(await sender.SendDeadlineEmailAsync(account.TenantId,account.UserId,"Chuẩn bị thiết bị",DateTimeOffset.UtcNow,"4h",timeout.Token));
        var message = await receive; Assert.Equal("employee@example.test", Assert.Single(message.To.Mailboxes).Address);
        Assert.Contains("Chuẩn bị thiết bị",message.TextBody); Assert.Contains("4h",message.TextBody);
    }
}
