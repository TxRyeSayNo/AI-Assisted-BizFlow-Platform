using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Notifications;
using BizFlow.Application.Organization;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.AI;
using BizFlow.Infrastructure.Notifications;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class NotificationCompletionTests(PostgresFixture database)
{
    private const string Password="Notification-owner!1834";
    private sealed class Clock:TimeProvider {public DateTimeOffset Now{get;set;}=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Updates:INotificationUpdates { public Task PublishAsync(Guid tenantId,IReadOnlyCollection<Guid> recipients,CancellationToken ct)=>Task.CompletedTask; }
    private sealed class Presence(bool online):IRealtimePresenceChecker { public bool IsUserOnline(Guid tenant,Guid user)=>online; }
    private sealed class Push:IWebPushDispatcher {
        public int Calls {get;private set;}
        public Task<WebPushDispatchResult> SendNotificationAsync(Guid tenant,Guid user,string title,string message,string? target,CancellationToken ct){Calls++;return Task.FromResult(new WebPushDispatchResult(false,false,"Unavailable"));}
    }
    private sealed class Email:IEmailNotificationDispatcher {
        public int Calls {get;private set;}
        public Task<bool> SendDeadlineEmailAsync(Guid tenant,Guid user,string title,DateTimeOffset? due,string trigger,CancellationToken ct){Calls++;return Task.FromResult(true);}
    }
    private AuthenticationFactory Factory(TcpListener? smtp=null,Clock? clock=null)=>new(database,smtp is null?null:new Dictionary<string,string?>{
        ["NotificationEmail:Enabled"]="true",["NotificationEmail:Host"]="127.0.0.1",["NotificationEmail:Port"]=((IPEndPoint)smtp.LocalEndpoint).Port.ToString(),
        ["NotificationEmail:AllowInsecureLoopback"]="true",["NotificationEmail:FromAddress"]="notify@example.test",["NotificationEmail:FrontendOrigin"]="https://bizflow.example.test"
    },configureServices:services=>{
        foreach(var descriptor in services.Where(s=>s.ServiceType==typeof(IHostedService)&&s.ImplementationType==typeof(NotificationEmailWorker)).ToArray())services.Remove(descriptor);
        if(clock is not null)services.AddSingleton<TimeProvider>(clock);
    });
    private async Task<(Guid Tenant,Guid User,string Key)> Seed()
    {
        var account=await database.SeedTenantAsync();
        await using var db=database.Create(account.UserId,account.TenantId);
        var user=await db.Users.SingleAsync();var tenant=await db.Tenants.SingleAsync();
        var hash=new PasswordHasher<UserAccount>().HashPassword(user,Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            INSERT INTO "UserRole" ("UserId","RoleId") SELECT {user.Id},"RoleId" FROM "Role" WHERE "IsSystem" AND "Name"='COMPANY_ADMIN';
            """);
        return(account.TenantId,account.UserId,tenant.TenantKey);
    }
    private static async Task Login(HttpClient client,string key,string identifier="EMP001",string password=Password)
    {
        using var response=await client.PostAsJsonAsync("/api/v1/auth/login",new{identifier,tenantKey=key,password});
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization=new("Bearer",(await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
    private static Dictionary<string,string> Fragment(string body)=>new Uri(body.Split('\n').Select(s=>s.Trim()).Single(s=>s.StartsWith("https://bizflow.example.test/reset-password#"))).Fragment.TrimStart('#')
        .Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1]));
    private static async Task<MimeMessage[]> Deliver(AuthenticationFactory factory,TcpListener smtp,Guid tenant,int count)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var receive=Enumerable.Range(0,count).Select(_=>ResetEmailTests.ReceiveAsync(smtp,timeout.Token)).ToArray();
        using var scope=factory.Services.CreateScope();
        Assert.Equal(count,await scope.ServiceProvider.GetRequiredService<NotificationEmailOutboxDispatcher>().SweepTenantAsync(tenant,timeout.Token));
        return await Task.WhenAll(receive);
    }

    [Fact]
    public async Task Single_and_bulk_created_employees_receive_private_one_time_password_setup_and_inbox_welcome()
    {
        var a=await Seed();using var smtp=new TcpListener(IPAddress.Loopback,0);smtp.Start();
        await using var factory=Factory(smtp);using var owner=factory.CreateClient();await Login(owner,a.Key);
        using var created=await owner.PostAsJsonAsync("/api/v1/users",new{employeeCode="NEW",email="new@example.test",fullName="New Employee",password="Ignored-shared!123"});
        Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());
        var single=(await created.Content.ReadFromJsonAsync<UserOperationResult>())!;
        var batch=new{users=new[]{new{employeeCode="BULK1",email="bulk1@example.test",fullName="Bulk One",password="Ignored-shared!123"},new{employeeCode="BULK2",email="bulk2@example.test",fullName="Bulk Two",password=""}},dryRun=true};
        using var dry=await owner.PostAsJsonAsync("/api/v1/users/batch-import",batch);Assert.True(dry.IsSuccessStatusCode,await dry.Content.ReadAsStringAsync());
        await using var db=database.Create(a.User,a.Tenant);
        Assert.Equal(0,await db.Users.CountAsync(u=>u.EmployeeCode.StartsWith("BULK")));
        using var imported=await owner.PostAsJsonAsync("/api/v1/users/batch-import",new{batch.users,dryRun=false});
        Assert.True(imported.IsSuccessStatusCode,await imported.Content.ReadAsStringAsync());
        Assert.All((await imported.Content.ReadFromJsonAsync<BatchImportUsersResult>())!.Items,item=>Assert.Null(item.ActivationToken));
        var users=await db.Users.Where(u=>u.Id!=a.User).ToArrayAsync();Assert.Equal(3,users.Length);Assert.All(users,u=>Assert.True(u.MustChangePassword));
        // A mail opt-out never suppresses the account setup invitation.
        await NotificationPreferenceStore.WriteAsync(db,a.Tenant,single.UserId,new(EmailEnabled:false),default);
        var messages=await Deliver(factory,smtp,a.Tenant,3);
        foreach(var user in users)
        {
            var message=Assert.Single(messages,m=>m.To.Mailboxes.Single().Address==user.Email);
            Assert.Contains(a.Key,message.TextBody);Assert.Contains(user.EmployeeCode,message.TextBody);Assert.DoesNotContain("Ignored-shared",message.TextBody);
            var fragment=Fragment(message.TextBody!);Assert.Equal(user.EmployeeCode,fragment["identifier"]);Assert.Equal(a.Key,fragment["tenantKey"]);
            using var client=factory.CreateClient();
            using var blocked=await client.PostAsJsonAsync("/api/v1/auth/login",new{identifier=user.EmployeeCode,tenantKey=a.Key,password="Ignored-shared!123"});Assert.Equal(HttpStatusCode.Unauthorized,blocked.StatusCode);
            using var reset=await client.PostAsJsonAsync("/api/v1/auth/reset-password",new{identifier=user.EmployeeCode,tenantKey=a.Key,resetToken=fragment["token"],newPassword="Chosen-by-employee!184"});Assert.True(reset.IsSuccessStatusCode,await reset.Content.ReadAsStringAsync());
            await Login(client,a.Key,user.EmployeeCode,"Chosen-by-employee!184");
            using var reuse=await client.PostAsJsonAsync("/api/v1/auth/reset-password",new{identifier=user.EmployeeCode,tenantKey=a.Key,resetToken=fragment["token"],newPassword="Another-password!184"});Assert.Equal(HttpStatusCode.Unauthorized,reuse.StatusCode);
            var inbox=await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");Assert.Contains(inbox.GetProperty("items").EnumerateArray(),n=>n.GetProperty("content").GetString()!.Contains(a.Key));
            Assert.DoesNotContain(fragment["token"],inbox.GetRawText());
        }
        using var scope=factory.Services.CreateScope();Assert.Equal(0,await scope.ServiceProvider.GetRequiredService<NotificationEmailOutboxDispatcher>().SweepTenantAsync(a.Tenant,default));
    }

    [Fact]
    public async Task Meeting_uses_one_post_commit_email_with_full_meeting_details_and_honors_opt_out()
    {
        var a=await Seed();using var smtp=new TcpListener(IPAddress.Loopback,0);smtp.Start();await using var factory=Factory(smtp);_ = factory.CreateClient();
        await using var db=database.Create(a.User,a.Tenant);
        var department=Department.Create(a.Tenant,"IT","Công nghệ",null,DateTimeOffset.UtcNow);db.Departments.Add(department);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DepartmentId\"={department.Id} WHERE \"UserId\"={a.User}");
        var coordinator=new AgentMeetingCoordinator(db,new Updates(),NullLogger<AgentMeetingCoordinator>.Instance,TimeProvider.System);
        var result=await coordinator.DispatchMeetingNotificationAsync(a.Tenant,a.User,"Kế hoạch quý","09:00 ngày 26/11/2026","Phòng họp A",["IT"],["Máy chiếu"],default);
        Assert.Equal(0,result.EmailsSent);Assert.Equal(1,result.EmailsQueued);
        Assert.False(smtp.Pending());
        var notice=await db.Notifications.SingleAsync();Assert.Equal("MeetingInvitation",notice.Type);
        var mail=Assert.Single(await Deliver(factory,smtp,a.Tenant,1));
        Assert.Contains("Thư mời họp",mail.Subject);Assert.DoesNotContain("Nhắc hạn",mail.Subject);Assert.Contains("Máy chiếu",mail.TextBody);Assert.Contains("Phòng họp A",mail.TextBody);
        await NotificationPreferenceStore.WriteAsync(db,a.Tenant,a.User,new(EmailEnabled:false),default);
        await coordinator.DispatchMeetingNotificationAsync(a.Tenant,a.User,"Họp lần hai","10:00","Phòng B",["IT"],null,default);
        using var scope=factory.Services.CreateScope();Assert.Equal(0,await scope.ServiceProvider.GetRequiredService<NotificationEmailOutboxDispatcher>().SweepTenantAsync(a.Tenant,default));
        Assert.Equal(2,await db.Notifications.CountAsync());Assert.False(smtp.Pending());
    }

    [Theory]
    [InlineData("Always",true,true,1,0)]
    [InlineData("AwayOnly",true,true,0,0)]
    [InlineData("AwayOnly",false,true,1,1)]
    [InlineData("Off",false,true,0,1)]
    [InlineData("Always",false,false,0,1)]
    public async Task Deadline_delivery_honors_personal_email_policy_and_preserves_the_inbox(string mode,bool online,bool enabled,int mailCount,int pushCount)
    {
        var a=await Seed();await using var db=database.Create(a.User,a.Tenant);
        var now=DateTimeOffset.UtcNow;var task=WorkTask.CreateDraft(a.Tenant,a.User,"Policy deadline",now,deadline:now.AddMinutes(-1));
        db.WorkTasks.Add(task);await db.SaveChangesAsync();db.TaskAssignments.Add(TaskAssignment.Create(task.Id,a.User,null,a.User,now));await db.SaveChangesAsync();
        await NotificationPreferenceStore.WriteAsync(db,a.Tenant,a.User,new(EmailEnabled:enabled,DeadlineEmailMode:mode),default);
        await using var factory=Factory();using var scope=factory.Services.CreateScope();
        var email=new Email();var push=new Push();
        var dispatcher=new DeadlineReminderDispatcher(scope.ServiceProvider.GetRequiredService<DbContextOptions<BizFlow.Infrastructure.Persistence.BizFlowDbContext>>(),new Updates(),new Presence(online),push,email,TimeProvider.System,NullLogger<DeadlineReminderDispatcher>.Instance);
        Assert.Equal(1,(await dispatcher.SweepTenantAsync(a.Tenant,default)).RemindersSent);
        Assert.Equal(mailCount,email.Calls);Assert.Equal(pushCount,push.Calls);
        Assert.Single(await db.Notifications.ToArrayAsync());
        Assert.Equal(0,(await dispatcher.SweepTenantAsync(a.Tenant,default)).RemindersSent);Assert.Equal(mailCount,email.Calls);
    }

    [Fact]
    public async Task Range_digests_send_mail_only_when_enabled_and_always_leave_an_inbox_summary()
    {
        var a=await Seed();using var smtp=new TcpListener(IPAddress.Loopback,0);smtp.Start();await using var factory=Factory(smtp);
        using var client=factory.CreateClient();await Login(client,a.Key);
        using var created=await client.PostAsJsonAsync("/api/v1/notifications/digests",new TaskDigestInput(new(2026,11,25),new(2026,11,25)));
        Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());
        var mail=Assert.Single(await Deliver(factory,smtp,a.Tenant,1));Assert.Contains("Tổng hợp công việc",mail.Subject);Assert.Contains("Không có công việc",mail.TextBody);
        await using var db=database.Create(a.User,a.Tenant);
        await NotificationPreferenceStore.WriteAsync(db,a.Tenant,a.User,new(DigestEmail:false),default);
        using var another=await client.PostAsJsonAsync("/api/v1/notifications/digests",new TaskDigestInput(new(2026,11,26),new(2026,11,26)));Assert.True(another.IsSuccessStatusCode);
        using var scope=factory.Services.CreateScope();Assert.Equal(0,await scope.ServiceProvider.GetRequiredService<NotificationEmailOutboxDispatcher>().SweepTenantAsync(a.Tenant,default));
        Assert.Equal(2,await db.Notifications.CountAsync());Assert.False(smtp.Pending());
    }

    [Fact]
    public async Task Digests_use_tenant_calendar_days_only_own_open_assigned_work_and_deduplicate_daily_and_range_requests()
    {
        var a=await Seed();var foreign=await Seed();var clock=new Clock{Now=DateTimeOffset.Parse("2026-11-25T00:59:00Z")};
        await using var factory=Factory();using var client=factory.CreateClient();await Login(client,a.Key);
        await using var db=database.Create(a.User,a.Tenant);
        var other=UserAccount.CreateTenantUser(a.Tenant,"OTHER","other@example.test","Other","placeholder",DateTimeOffset.UtcNow);db.Users.Add(other);await db.SaveChangesAsync();
        var definitions=new[]{("today-start",DateTimeOffset.Parse("2026-11-24T17:00:00Z"),a.User,false),("today-end",DateTimeOffset.Parse("2026-11-25T16:59:00Z"),a.User,false),
            ("tomorrow",DateTimeOffset.Parse("2026-11-25T17:00:00Z"),a.User,false),("closed-secret",DateTimeOffset.Parse("2026-11-25T02:00:00Z"),a.User,true),
            ("other-secret",DateTimeOffset.Parse("2026-11-25T02:00:00Z"),other.Id,false)};
        foreach(var (title,due,user,closed) in definitions)
        {
            var task=WorkTask.CreateDraft(a.Tenant,a.User,title,DateTimeOffset.UtcNow,deadline:due);db.WorkTasks.Add(task);await db.SaveChangesAsync();
            db.TaskAssignments.Add(TaskAssignment.Create(task.Id,a.User,null,user,DateTimeOffset.UtcNow));await db.SaveChangesAsync();
            if(closed)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='COMPLETED' WHERE \"TaskId\"={task.Id}");
        }
        var prefs=new NotificationPreferencesView(DailyDigestEnabled:true,DigestHour:8,DigestDays:1);
        using var saved=await client.PutAsJsonAsync("/api/v1/notifications/preferences",prefs);Assert.True(saved.IsSuccessStatusCode,await saved.Content.ReadAsStringAsync());
        using var scope=factory.Services.CreateScope();var service=new TaskDigestService(scope.ServiceProvider.GetRequiredService<DbContextOptions<BizFlow.Infrastructure.Persistence.BizFlowDbContext>>(),clock,new Updates(),NullLogger<TaskDigestService>.Instance);
        Assert.Equal(0,await service.SweepAsync(default));clock.Now=clock.Now.AddMinutes(1);
        Assert.Equal(1,await service.SweepAsync(default));Assert.Equal(0,await service.SweepAsync(default));
        var daily=await db.Notifications.SingleAsync();Assert.Contains("2 công việc",daily.Content);Assert.Contains("today-start",daily.Content);Assert.Contains("today-end",daily.Content);
        Assert.DoesNotContain("tomorrow",daily.Content);Assert.DoesNotContain("secret",daily.Content);
        var input=new TaskDigestInput(new(2026,11,25),new(2026,11,26));
        var results=await Task.WhenAll(client.PostAsJsonAsync("/api/v1/notifications/digests",input),client.PostAsJsonAsync("/api/v1/notifications/digests",input));
        foreach(var result in results)Assert.True(result.IsSuccessStatusCode,await result.Content.ReadAsStringAsync());
        var bodies=await Task.WhenAll(results.Select(r=>r.Content.ReadFromJsonAsync<JsonElement>()));Assert.Single(bodies,b=>b.GetProperty("created").GetBoolean());
        Assert.Equal(3,bodies.Single(b=>b.GetProperty("created").GetBoolean()).GetProperty("result").GetProperty("taskCount").GetInt32());foreach(var result in results)result.Dispose();
        using var invalid=await client.PostAsJsonAsync("/api/v1/notifications/digests",new TaskDigestInput(new(2026,11,25),new(2027,1,1)));Assert.Equal(HttpStatusCode.UnprocessableEntity,invalid.StatusCode);
        using var badPreference=await client.PutAsJsonAsync("/api/v1/notifications/preferences",prefs with{DigestHour=24});Assert.Equal(HttpStatusCode.UnprocessableEntity,badPreference.StatusCode);
        await using var foreignDb=database.Create(foreign.User,foreign.Tenant);Assert.Empty(await foreignDb.Notifications.ToArrayAsync());
        Assert.False((await NotificationPreferenceStore.ReadAsync(foreignDb,foreign.Tenant,foreign.User,default)).DailyDigestEnabled);
        // A preference cannot be reassigned to another tenant even by bypassing the API.
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>foreignDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"NotificationPreference\" SET \"TenantId\"={foreign.Tenant} WHERE \"UserId\"={a.User}"));Assert.Equal("23514",error.SqlState);
    }
}
