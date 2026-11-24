using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BizFlow.Domain.Notifications;
using BizFlow.Infrastructure.Notifications;
using BizFlow.Infrastructure.Persistence;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class WebPushTransportTests(PostgresFixture database)
{
    [Fact]
    public async Task Encrypted_delivery_is_scoped_and_expired_endpoints_are_removed_without_losing_other_accounts()
    {
        var account = await database.SeedTenantAsync();
        var foreign = await database.SeedTenantAsync();
        using var receiver = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var receiverPublic = Public(receiver.ExportParameters(false));
        var auth = RandomNumberGenerator.GetBytes(16);
        var vapidParameters = vapid.ExportParameters(true);
        var values = new Dictionary<string,string?> {
            ["WebPush:Enabled"]="true",["WebPush:Subject"]="mailto:admin@example.test",
            ["WebPush:VapidPublicKey"]=Encode(Public(vapidParameters)),["WebPush:VapidPrivateKey"]=Encode(vapidParameters.D!),
            ["WebPush:AllowedHosts:0"]="push.example.test"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.True(configuration.GetSection("WebPush").Get<WebPushSettings>()!.IsValid);
        await using (var db = database.Create(account.UserId,account.TenantId)) {
            db.WebPushSubscriptions.Add(WebPushSubscription.Create(account.TenantId,account.UserId,"https://push.example.test/one",Encode(receiverPublic),Encode(auth),DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            var stored=await db.WebPushSubscriptions.SingleAsync();
            stored.RefreshKeys(Encode(receiverPublic),Encode(auth),DateTimeOffset.UtcNow.AddMinutes(1));
            await db.SaveChangesAsync();
        }
        await using (var db = database.Create(foreign.UserId,foreign.TenantId)) {
            db.WebPushSubscriptions.Add(WebPushSubscription.Create(foreign.TenantId,foreign.UserId,"https://push.example.test/foreign",Encode(receiverPublic),Encode(auth),DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await using (var foreignDb = database.Create(foreign.UserId,foreign.TenantId))
        await using (var ownDb = database.Create(account.UserId,account.TenantId)) {
            var foreignSubscription=await foreignDb.WebPushSubscriptions.AsNoTracking().SingleAsync();
            ownDb.WebPushSubscriptions.Remove(foreignSubscription);
            await Assert.ThrowsAsync<BizFlow.Application.Common.ApplicationFault>(()=>ownDb.SaveChangesAsync());
        }
        var transport = new Transport();
        var options = new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(database.ConnectionString).Options;
        var sender = new WebPushDispatcher(configuration,NullLogger<WebPushDispatcher>.Instance,options,transport);
        Assert.False((await sender.SendNotificationAsync(account.TenantId,foreign.UserId,"Secret","Private","/tasks/secret",default)).Delivered);
        Assert.Empty(transport.Messages);
        Assert.True((await sender.SendNotificationAsync(account.TenantId,account.UserId,"Secret","Private","/tasks/secret",default)).Delivered);
        var sent = Assert.Single(transport.Messages);
        Assert.Equal("aes128gcm",sent.Encoding);
        Assert.StartsWith("vapid ",sent.Authorization);
        var plain = Decrypt(sent.Body,receiver,receiverPublic,auth);
        using var payload = JsonDocument.Parse(plain);
        Assert.Equal("BizFlow",payload.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Secret",plain); Assert.DoesNotContain("Private",plain);
        Assert.Equal("/tasks/secret",payload.RootElement.GetProperty("data").GetProperty("url").GetString());
        transport.Status = HttpStatusCode.ServiceUnavailable;
        Assert.False((await sender.SendNotificationAsync(account.TenantId,account.UserId,"","",null,default)).Delivered);
        await using (var db = database.Create(account.UserId,account.TenantId)) Assert.Single(await db.WebPushSubscriptions.ToListAsync());
        transport.Status = HttpStatusCode.Gone;
        Assert.True((await sender.SendNotificationAsync(account.TenantId,account.UserId,"","",null,default)).SubscriptionExpired);
        await using (var db = database.Create(account.UserId,account.TenantId)) Assert.Empty(await db.WebPushSubscriptions.ToListAsync());
        await using (var db = database.Create(foreign.UserId,foreign.TenantId)) Assert.Single(await db.WebPushSubscriptions.ToListAsync());
    }

    [Fact]
    public void Configuration_rejects_mismatched_keys_and_untrusted_endpoints()
    {
        using var first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var second = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var settings = new WebPushSettings { Subject="mailto:admin@example.test",VapidPublicKey=Encode(Public(first.ExportParameters(false))),VapidPrivateKey=Encode(second.ExportParameters(true).D!) };
        Assert.False(settings.IsValid);
        Assert.True(settings.AllowsEndpoint("https://fcm.googleapis.com/push/123"));
        Assert.False(settings.AllowsEndpoint("https://fcm.googleapis.com.attacker.test/push"));
        Assert.False(settings.AllowsEndpoint("http://fcm.googleapis.com/push"));
        Assert.False(settings.AllowsEndpoint("https://127.0.0.1/push"));
        Assert.False(settings.AllowsEndpoint("https://fcm.googleapis.com:444/push"));
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static byte[] Public(ECParameters value) => [4,..value.Q.X!,..value.Q.Y!];
    private static string Decrypt(byte[] body,ECDiffieHellman receiver,byte[] receiverPublic,byte[] auth)
    {
        Assert.Equal(65,body[20]);
        var senderPublic=body[21..86];
        using var sender=ECDiffieHellman.Create(new ECParameters { Curve=ECCurve.NamedCurves.nistP256,Q=new ECPoint{X=senderPublic[1..33],Y=senderPublic[33..65]} });
        var shared=receiver.DeriveRawSecretAgreement(sender.PublicKey);
        var authPrk=HMACSHA256.HashData(auth,shared);
        var ikm=Expand(authPrk,[..Encoding.UTF8.GetBytes("WebPush: info\0"),..receiverPublic,..senderPublic],32);
        var prk=HMACSHA256.HashData(body[..16],ikm);
        var key=Expand(prk,Encoding.UTF8.GetBytes("Content-Encoding: aes128gcm\0"),16);
        var nonce=Expand(prk,Encoding.UTF8.GetBytes("Content-Encoding: nonce\0"),12);
        var cipher=body[86..^16];var plain=new byte[cipher.Length];
        using var aes=new AesGcm(key,16);aes.Decrypt(nonce,cipher,body[^16..],plain);
        var end=plain.Length-1;while(end>=0 && plain[end]==0)end--;
        Assert.Equal(2,plain[end]);
        return Encoding.UTF8.GetString(plain[..end]);
    }
    private static byte[] Expand(byte[] key,byte[] info,int size) => HMACSHA256.HashData(key,(byte[])[..info,1])[..size];
    private sealed class Transport : HttpMessageHandler,IHttpClientFactory
    {
        public HttpStatusCode Status {get;set;}=HttpStatusCode.Created;
        public List<(byte[] Body,string Encoding,string Authorization)> Messages {get;}=[];
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Messages.Add((await request.Content!.ReadAsByteArrayAsync(cancellationToken),string.Join(",",request.Content.Headers.ContentEncoding),request.Headers.GetValues("Authorization").Single()));
            return new HttpResponseMessage(Status){Content=new StringContent("")};
        }
    }
}
