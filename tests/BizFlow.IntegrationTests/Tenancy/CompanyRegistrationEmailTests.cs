using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Notifications;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Tenancy;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MimeKit;

namespace BizFlow.IntegrationTests.Tenancy;

[Collection("Postgres")]
public sealed class CompanyRegistrationEmailTests(PostgresFixture database)
{
    private const string Password="Platform-password!184";
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now{get;set;}=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private AuthenticationFactory Factory(TcpListener smtp,Clock clock)=>new(database,new Dictionary<string,string?> {
        ["ResetEmail:Enabled"]="false",["NotificationEmail:Enabled"]="true",["NotificationEmail:Host"]="127.0.0.1",
        ["NotificationEmail:Port"]=((IPEndPoint)smtp.LocalEndpoint).Port.ToString(),["NotificationEmail:AllowInsecureLoopback"]="true",
        ["NotificationEmail:FromAddress"]="notify@example.test",["NotificationEmail:FrontendOrigin"]="https://bizflow.example.test"
    },configureServices:services=>{
        foreach(var descriptor in services.Where(s=>s.ServiceType==typeof(IHostedService)&&s.ImplementationType==typeof(NotificationEmailWorker)).ToArray())services.Remove(descriptor);
        services.AddSingleton<TimeProvider>(clock);
    });
    private async Task Platform(HttpClient client)
    {
        var code="PLATFORM-"+Guid.NewGuid().ToString("N");
        var user=UserAccount.CreatePlatformAdministrator(code,code+"@example.test","Approver","placeholder",DateTimeOffset.UtcNow);
        user.ResetPassword(new PasswordHasher<UserAccount>().HashPassword(user,Password),DateTimeOffset.UtcNow);
        await using var db=database.Create(database.PlatformUserId,null);db.Users.Add(user);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"UserRole\" (\"UserId\",\"RoleId\") SELECT {user.Id},\"RoleId\" FROM \"Role\" WHERE \"Name\"='PLATFORM_ADMIN' AND \"IsSystem\"");
        using var login=await client.PostAsJsonAsync("/api/v1/auth/login",new{identifier=code,password=Password});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",(await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
    private static SubmitCompanyRegistrationRequest Request(string code)=>new(code,"Công ty thử nghiệm","0101234567","Người đăng ký","contact-"+code+"@example.test","0901234567");
    private static async Task<SubmitCompanyRegistrationResult> Register(HttpClient client,SubmitCompanyRegistrationRequest request)
    {
        using var response=await client.PostAsJsonAsync("/api/v1/company-registrations",request);
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SubmitCompanyRegistrationResult>())!;
    }
    private static async Task<MimeMessage[]> Deliver(AuthenticationFactory factory,TcpListener smtp,Guid company,int count)
    {
        ((Clock)factory.Services.GetRequiredService<TimeProvider>()).Now=DateTimeOffset.UtcNow;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var incoming=Enumerable.Range(0,count).Select(_=>ResetEmailTests.ReceiveAsync(smtp,timeout.Token)).ToArray();
        using var first=factory.Services.CreateScope();using var second=factory.Services.CreateScope();
        var delivered=await Task.WhenAll(first.ServiceProvider.GetRequiredService<CompanyRegistrationEmailDispatcher>().SweepAsync(timeout.Token,company),
            second.ServiceProvider.GetRequiredService<CompanyRegistrationEmailDispatcher>().SweepAsync(timeout.Token,company));
        Assert.Equal(count,delivered.Sum());
        return await Task.WhenAll(incoming);
    }
    private static string Link(MimeMessage message,string path)=>message.TextBody!.Split('\n').Select(x=>x.Trim()).Single(x=>x.StartsWith("https://bizflow.example.test/"+path,StringComparison.Ordinal));
    private static Dictionary<string,string> Fragment(string url)=>new Uri(url).Fragment.TrimStart('#').Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1]));

    [Fact]
    public async Task Approval_sends_workspace_account_and_one_time_password_setup_only_to_account_owner()
    {
        using var smtp=new TcpListener(IPAddress.Loopback,0);smtp.Start();var clock=new Clock();
        await using var factory=Factory(smtp,clock);using var applicant=factory.CreateClient();using var platform=factory.CreateClient();
        await Platform(platform);var request=Request("mail-"+Guid.NewGuid().ToString("N"));var registration=await Register(applicant,request);
        var receipt=Assert.Single(await Deliver(factory,smtp,registration.CompanyId,1));
        Assert.Equal(request.ContactEmail,Assert.Single(receipt.To.Mailboxes).Address);Assert.Contains("tiếp nhận",receipt.Subject);
        using var approved=await platform.PostAsJsonAsync($"/api/v1/platform/company-registrations/{registration.CompanyId}/approve",new {
            tenantKey=request.Code,adminEmployeeCode="ADMIN01",adminEmail="owner-"+request.Code+"@example.test",adminFullName="Quản trị doanh nghiệp",adminPassword="Old-default-password!"
        });
        Assert.True(approved.IsSuccessStatusCode,await approved.Content.ReadAsStringAsync());
        var result=(await approved.Content.ReadFromJsonAsync<ApproveCompanyRegistrationResult>())!;
        var messages=await Deliver(factory,smtp,registration.CompanyId,2);
        var invite=Assert.Single(messages,m=>m.To.Mailboxes.Single().Address.StartsWith("owner-"));
        var contact=Assert.Single(messages,m=>m.To.Mailboxes.Single().Address==request.ContactEmail);
        Assert.DoesNotContain("#token=",contact.TextBody);Assert.Contains(request.Code,contact.TextBody);
        Assert.Contains("Workspace-key: "+request.Code,invite.TextBody);Assert.Contains("ADMIN01",invite.TextBody);
        Assert.DoesNotContain("Old-default-password!",invite.TextBody);
        var fragment=Fragment(Link(invite,"reset-password"));
        Assert.Equal(request.Code,fragment["tenantKey"]);Assert.Equal("ADMIN01",fragment["identifier"]);
        await using var db=database.Create(result.AdminUserId,result.TenantId);
        Assert.True((await db.Users.SingleAsync()).MustChangePassword);
        using var blocked=await applicant.PostAsJsonAsync("/api/v1/auth/login",new{identifier="ADMIN01",tenantKey=request.Code,password="Old-default-password!"});
        Assert.Equal(HttpStatusCode.Unauthorized,blocked.StatusCode);
        using var setPassword=await applicant.PostAsJsonAsync("/api/v1/auth/reset-password",new{identifier="ADMIN01",tenantKey=request.Code,resetToken=fragment["token"],newPassword="Chosen-password!924"});
        Assert.True(setPassword.IsSuccessStatusCode,await setPassword.Content.ReadAsStringAsync());
        using var reused=await applicant.PostAsJsonAsync("/api/v1/auth/reset-password",new{identifier="ADMIN01",tenantKey=request.Code,resetToken=fragment["token"],newPassword="Other-password!924"});
        Assert.Equal(HttpStatusCode.Unauthorized,reused.StatusCode);
        using var login=await applicant.PostAsJsonAsync("/api/v1/auth/login",new{identifier="ADMIN01",tenantKey=request.Code,password="Chosen-password!924"});Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        using var scope=factory.Services.CreateScope();
        Assert.Equal(0,await scope.ServiceProvider.GetRequiredService<CompanyRegistrationEmailDispatcher>().SweepAsync(default,registration.CompanyId));
        var stored=await db.Database.SqlQuery<string>($"SELECT \"Payload\"::text AS \"Value\" FROM \"CompanyRegistrationEmail\" WHERE \"CompanyId\"={registration.CompanyId}").ToArrayAsync();
        Assert.All(stored,text=>{Assert.DoesNotContain(fragment["token"],text);Assert.DoesNotContain("Old-default-password",text);});
    }

    [Fact]
    public async Task Rejection_preserves_reason_and_signed_resubmission_allows_one_update_without_creating_a_tenant()
    {
        using var smtp=new TcpListener(IPAddress.Loopback,0);smtp.Start();var clock=new Clock();
        await using var factory=Factory(smtp,clock);using var applicant=factory.CreateClient();using var platform=factory.CreateClient();
        await Platform(platform);var request=Request("fix-"+Guid.NewGuid().ToString("N"));var registration=await Register(applicant,request);
        await Deliver(factory,smtp,registration.CompanyId,1);
        const string reason="Thiếu bản đăng ký kinh doanh. Vui lòng bổ sung bản rõ nét và số điện thoại liên hệ.";
        using var rejected=await platform.PostAsJsonAsync($"/api/v1/platform/company-registrations/{registration.CompanyId}/reject",new{reason});
        Assert.True(rejected.IsSuccessStatusCode,await rejected.Content.ReadAsStringAsync());
        var mail=Assert.Single(await Deliver(factory,smtp,registration.CompanyId,1));Assert.Contains(reason,mail.TextBody);
        var token=Fragment(Link(mail,"register-company"))["resubmit"];
        using var invalid=await applicant.PostAsJsonAsync("/api/v1/company-registrations/resubmission",new{token=token+"tampered"});Assert.Equal(HttpStatusCode.NotFound,invalid.StatusCode);
        using var resolve=await applicant.PostAsJsonAsync("/api/v1/company-registrations/resubmission",new{token});Assert.Equal(HttpStatusCode.OK,resolve.StatusCode);
        var corrected=request with{ContactPhone="0912345678",ResubmissionToken=token};
        using var hijack=await applicant.PostAsJsonAsync("/api/v1/company-registrations",corrected with{ContactEmail="attacker@example.test"});Assert.Equal(HttpStatusCode.NotFound,hijack.StatusCode);
        var responses=await Task.WhenAll(applicant.PostAsJsonAsync("/api/v1/company-registrations",corrected),applicant.PostAsJsonAsync("/api/v1/company-registrations",corrected));
        Assert.Single(responses,r=>r.IsSuccessStatusCode);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.NotFound);
        var result=(await responses.Single(r=>r.IsSuccessStatusCode).Content.ReadFromJsonAsync<SubmitCompanyRegistrationResult>())!;
        Assert.Equal(registration.CompanyId,result.CompanyId);Assert.Equal("PENDING",result.Status);Assert.Equal("0912345678",result.ContactPhone);
        foreach(var response in responses)response.Dispose();
        await using var db=database.Create(database.PlatformUserId,null);
        Assert.False(await db.Tenants.IgnoreQueryFilters().AnyAsync(t=>t.CompanyId==registration.CompanyId));
        Assert.Single(await db.Companies.IgnoreQueryFilters().Where(c=>c.Code==request.Code.ToUpperInvariant()).ToArrayAsync());
        using var replay=await applicant.PostAsJsonAsync("/api/v1/company-registrations/resubmission",new{token});Assert.Equal(HttpStatusCode.NotFound,replay.StatusCode);
        var audit=await db.AuditLogs.IgnoreQueryFilters().Where(a=>a.ObjectId==registration.CompanyId).ToArrayAsync();Assert.Contains(audit,a=>a.Action.Contains("REJECT",StringComparison.OrdinalIgnoreCase));
        var receipt=Assert.Single(await Deliver(factory,smtp,registration.CompanyId,1));Assert.Contains("tiếp nhận",receipt.Subject);
        // Fresh rejection links also expire even if the company remains rejected.
        using var rejectAgain=await platform.PostAsJsonAsync($"/api/v1/platform/company-registrations/{registration.CompanyId}/reject",new{reason});Assert.True(rejectAgain.IsSuccessStatusCode);
        using var oldLink=await applicant.PostAsJsonAsync("/api/v1/company-registrations/resubmission",new{token});Assert.Equal(HttpStatusCode.NotFound,oldLink.StatusCode);
        var again=Assert.Single(await Deliver(factory,smtp,registration.CompanyId,1));var expired=Fragment(Link(again,"register-company"))["resubmit"];
        clock.Now=clock.Now.AddDays(8);
        using var expiredResult=await applicant.PostAsJsonAsync("/api/v1/company-registrations/resubmission",new{token=expired});Assert.Equal(HttpStatusCode.NotFound,expiredResult.StatusCode);
    }
}
