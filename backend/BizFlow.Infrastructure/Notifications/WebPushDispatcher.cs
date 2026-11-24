using System.Net;
using System.Text.Json;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Notifications;

public sealed class WebPushDispatcher(IConfiguration configuration,ILogger<WebPushDispatcher> logger,
    DbContextOptions<BizFlowDbContext> options,IHttpClientFactory clients) : IWebPushDispatcher
{
    public const string ClientName="BizFlow.WebPush";
    private sealed record RecipientContext(Guid Tenant,Guid User) : ITenantContext
    { public Guid? TenantId=>Tenant; public Guid? UserId=>User; }
    public async Task<WebPushDispatchResult> SendNotificationAsync(
        Guid tenantId,
        Guid userId,
        string title,
        string message,
        string? targetUrl,
        CancellationToken cancellationToken)
    {
        var settings=configuration.GetSection("WebPush").Get<WebPushSettings>();
        if(settings is not {Enabled:true} || !settings.IsValid)return new(false,false,"Web push is not configured.");
        await using var db=new BizFlowDbContext(options,new RecipientContext(tenantId,userId));
        if(!await db.Users.AnyAsync(u=>u.Id==userId && u.Status==UserStatus.Active && u.DeletedAt==null,cancellationToken))return new(false,false,"Recipient is unavailable.");
        var subscriptions=await db.WebPushSubscriptions.Where(s=>s.UserId==userId).ToListAsync(cancellationToken);
        if(subscriptions.Count==0)return new(false,false,"No browser subscription.");
        using var http=clients.CreateClient(ClientName);
        var client=new PushServiceClient(http){AutoRetryAfter=false,DefaultTimeToLive=3600};
        using var authentication=new VapidAuthentication(settings.VapidPublicKey,settings.VapidPrivateKey){Subject=settings.Subject};
        var url=targetUrl is {Length:>0} && targetUrl.StartsWith('/') && !targetUrl.StartsWith("//")?targetUrl:"/notifications";
        // Devices can be shared. Detailed work content is available only after the app checks the signed-in account.
        var payload=JsonSerializer.Serialize(new{title="BizFlow",body="Bạn có thông báo công việc mới. Mở BizFlow để xem chi tiết.",data=new{url}});
        var delivered=false;var expired=0;
        foreach(var stored in subscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(!settings.AllowsEndpoint(stored.Endpoint))continue;
            var subscription=new PushSubscription{Endpoint=stored.Endpoint,Keys=new Dictionary<string,string>{{"p256dh",stored.P256dh},{"auth",stored.Auth}}};
            try
            {
                await client.RequestPushMessageDeliveryAsync(subscription,new PushMessage(payload),authentication,cancellationToken);
                delivered=true;
            }
            catch(PushServiceClientException error) when(error.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            { db.WebPushSubscriptions.Remove(stored);expired++; }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
            catch(Exception)
            { logger.LogWarning("Web push delivery failed for tenant {TenantId}, user {UserId}.",tenantId,userId); }
        }
        if(expired>0)await db.SaveChangesAsync(cancellationToken);
        return new(delivered,!delivered && expired==subscriptions.Count,delivered?null:"No push service accepted this notification.");
    }
}
