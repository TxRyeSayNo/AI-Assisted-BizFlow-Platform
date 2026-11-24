using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Workflows;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BizFlow.Domain.Security;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BizFlow.Infrastructure.Workflows;

namespace BizFlow.IntegrationTests.Workflows;

[Collection("Postgres")]
public sealed partial class WorkflowRuntimeTests(PostgresFixture database)
{
    private const string Password = "Workflow-tests!72591";
    private sealed record Account(Guid Tenant, Guid Requester, Guid Approver, string Key, Guid Service, Guid Category, Guid Version);

    private async Task<Account> Seed(string policy = "SINGLE", bool selfApproval = false, bool parallel = false, bool matrix = false, string? agentMode = null)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var now = DateTimeOffset.UtcNow;
        var tenant = await db.Tenants.SingleAsync(); var requester = await db.Users.SingleAsync();
        var dept = Department.Create(seed.TenantId, "OPS", "Vận hành", null, now);
        db.Departments.Add(dept); await db.SaveChangesAsync();
        var approver = UserAccount.CreateTenantUser(seed.TenantId, "APPROVER", "approver@example.test", "Người phê duyệt", "fixture", now, dept.Id);
        db.Users.Add(approver);
        var admin = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
        var employee = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        db.UserRoles.Add(new(requester.Id, admin.Id)); db.UserRoles.Add(new(approver.Id, employee.Id));
        var second = UserAccount.CreateTenantUser(seed.TenantId, "SECOND", "second@example.test", "Người duyệt thứ hai", "fixture", now, dept.Id);
        db.Users.Add(second); db.UserRoles.Add(new(second.Id, employee.Id));
        var lowRole = Role.CreateCustom(seed.TenantId,"Low value approver",now);
        if (matrix) { db.Roles.Add(lowRole); db.UserRoles.Add(new(approver.Id,lowRole.Id)); }
        await db.SaveChangesAsync();
        foreach (var user in new[] { requester, approver, second })
        {
            var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Company\" SET \"Status\"='ACTIVE' WHERE \"CompanyId\"={tenant.CompanyId}");
        var service = InternalService.Create(seed.TenantId, "ONBOARD", "Tiếp nhận nhân sự", null, true, now);
        db.Services.Add(service); await db.SaveChangesAsync();
        var category = ServiceCategory.Create(service.Id, "NEW", "Nhân sự mới"); db.ServiceCategories.Add(category); await db.SaveChangesAsync();
        var workflow = WorkflowDefinition.CreateDraft(seed.TenantId, "Quy trình tiếp nhận", WorkflowBusinessType.Request, now);
        db.Workflows.Add(workflow); await db.SaveChangesAsync();
        var version = WorkflowVersion.CreateDraft(workflow.Id, 1);
        version.UpdateDefinition(JsonSerializer.Serialize(new {
            nodes = new object[] {
                new { id = "start", type = "START", name = "Bắt đầu" },
                new { id = "approve", type = "APPROVAL", name = "Duyệt", assignmentMethod = policy == "ALL" ? "BUSINESS_ROLE" : "SPECIFIC_USER", roleId = employee.Id, specificUserId = selfApproval ? requester.Id : approver.Id,
                    approvalCountPolicy = policy, slaHours = 8, requireNotes = true },
                new { id = "confirm", type = "CONFIRMATION", name = "Xác nhận", assignmentMethod = "REQUESTER" },
                new { id = "end", type = "END", name = "Hoàn tất", completionState = "COMPLETED" } },
            edges = new[] { new {source="start",target="approve"}, new {source="approve",target="confirm"}, new {source="confirm",target="end"} },
            formFields = new[] { new {key="budget",label="Dự toán",type="number",required=true,min=0,max=100000} }
        }));
        if (matrix)
        {
            var graph = System.Text.Json.Nodes.JsonNode.Parse(version.DefinitionJson)!.AsObject();
            var approval = graph["nodes"]!.AsArray().Single(n=>n!["id"]!.GetValue<string>()=="approve")!;
            approval["assignmentMethod"]="MATRIX";
            approval["approvalMatrix"]=JsonSerializer.SerializeToNode(new WorkflowApprovalMatrixConfig("budget",
                [new(1000,lowRole.Id,"ANY"),new(null,employee.Id,"ALL")]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            version.UpdateDefinition(graph.ToJsonString());
        }
        if (agentMode is not null)
        {
            var graph = System.Text.Json.Nodes.JsonNode.Parse(version.DefinitionJson)!.AsObject();
            graph["nodes"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { id="agent", type="DEPARTMENT", name="AI chuẩn bị hồ sơ", assignmentMethod="SPECIFIC_USER", specificUserId=approver.Id, slaHours=8,
                agent=new WorkflowAgentPolicy(agentMode,"Tóm tắt dự toán và chuẩn bị kết quả bàn giao.",["budget"]) },new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            graph["edges"] = JsonSerializer.SerializeToNode(new[] {new{source="start",target="agent"},new{source="agent",target="approve"},new{source="approve",target="confirm"},new{source="confirm",target="end"}});
            version.UpdateDefinition(graph.ToJsonString());
        }
        if (parallel)
        {
            var graph = System.Text.Json.Nodes.JsonNode.Parse(version.DefinitionJson)!.AsObject();
            var nodes = graph["nodes"]!.AsArray();
            nodes.Add(System.Text.Json.Nodes.JsonNode.Parse("""{"id":"split","type":"PARALLEL_SPLIT","name":"Tách việc"}"""));
            nodes.Add(System.Text.Json.Nodes.JsonNode.Parse("""{"id":"join","type":"PARALLEL_JOIN","name":"Chờ đủ"}"""));
            nodes.Add(JsonSerializer.SerializeToNode(new {id="equipment",type="DEPARTMENT",name="Chuẩn bị thiết bị",assignmentMethod="SPECIFIC_USER",specificUserId=second.Id}));
            graph["edges"] = JsonSerializer.SerializeToNode(new[] {new {source="start",target="split"},new {source="split",target="approve"},new {source="split",target="equipment"},
                new {source="approve",target="join"},new {source="equipment",target="join"},new {source="join",target="confirm"},new {source="confirm",target="end"}});
            version.UpdateDefinition(graph.ToJsonString());
        }
        db.WorkflowVersions.Add(version); await db.SaveChangesAsync();
        version.Publish(now); workflow.Activate(now); await db.SaveChangesAsync();
        service.SetActiveWorkflowVersion(version.Id, now); await db.SaveChangesAsync();
        return new(seed.TenantId, requester.Id, approver.Id, tenant.TenantKey, service.Id, category.Id, version.Id);
    }
    private static async Task<HttpClient> Login(AuthenticationFactory factory, Account account, bool approver = false, string? identifier = null)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new {identifier=identifier ?? (approver ? "APPROVER" : "EMP001"),password=Password,tenantKey=account.Key});
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var login = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login!.AccessToken);
        return client;
    }
    private static Task<HttpResponseMessage> Create(HttpClient client, Account account, object? data = null) => client.PostAsJsonAsync("/api/v1/requests", new {
        serviceId=account.Service,categoryId=account.Category,title="Chuẩn bị nhân sự",description="Tiếp nhận ngày đầu",priority="MEDIUM",submitImmediately=true,formData=data ?? new {budget=1200}
    });

    private async Task<Guid> AddBackup(Account account)
    {
        await using var db = database.Create(account.Requester, account.Tenant);
        var user = UserAccount.CreateTenantUser(account.Tenant, "BACKUP", "backup@example.test", "Người thay thế", "fixture", DateTimeOffset.UtcNow);
        var role = Role.CreateCustom(account.Tenant, "Backup reader", DateTimeOffset.UtcNow);
        db.AddRange(user, role); await db.SaveChangesAsync();
        var read = await db.Permissions.SingleAsync(p => p.Code == "requests.read.own");
        db.RolePermissions.Add(new(role.Id, read.Id)); db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        return user.Id;
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Deadline_sweeps_deduplicate_concurrent_runs_alert_the_right_people_and_skip_paused_steps()
    {
        var account=await Seed();var clock=new MutableClock(DateTimeOffset.UtcNow);
        await using var factory=new AuthenticationFactory(database,configureServices:s=>s.AddSingleton<TimeProvider>(clock));
        using var owner=await Login(factory,account);using var approver=await Login(factory,account,true);
        using var created=await Create(owner,account);Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run=(await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        using var pausedCreation=await Create(owner,account);Assert.True(pausedCreation.IsSuccessStatusCode,await pausedCreation.Content.ReadAsStringAsync());
        var pausedId=(await pausedCreation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var pausedRun=(await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{pausedId}/workflow"))!;
        using var pause=await approver.PostAsJsonAsync($"/api/v1/requests/{pausedId}/workflow/steps/{pausedRun.Steps.Single().Id}/sla/pause",new WorkflowSlaInput(pausedRun.Revision,"Chờ bổ sung"));
        Assert.True(pause.IsSuccessStatusCode,await pause.Content.ReadAsStringAsync());
        clock.Now=run.Steps.Single().DueAt!.Value.AddMinutes(-30);
        using var scope1=factory.Services.CreateScope();using var scope2=factory.Services.CreateScope();
        var first=scope1.ServiceProvider.GetRequiredService<IWorkflowDeadlineDispatcher>();var second=scope2.ServiceProvider.GetRequiredService<IWorkflowDeadlineDispatcher>();
        var concurrent=await Task.WhenAll(first.SweepTenantAsync(account.Tenant,default),second.SweepTenantAsync(account.Tenant,default));
        Assert.Equal(1,concurrent.Sum());
        clock.Now=run.Steps.Single().DueAt!.Value.AddHours(25);
        Assert.Equal(2,await first.SweepTenantAsync(account.Tenant,default));
        Assert.Equal(0,await second.SweepTenantAsync(account.Tenant,default));
        await using var db=database.Create(account.Requester,account.Tenant);
        var notices=await db.Notifications.IgnoreQueryFilters().Where(n=>n.TenantId==account.Tenant && n.IdempotencyKey.StartsWith("wf:")).ToListAsync();
        Assert.Equal(3,notices.Count);Assert.All(notices,n=>Assert.Equal(id,n.ObjectId));
        Assert.Contains(notices,n=>n.RecipientId==account.Approver && n.Type=="DeadlineWarning");
        Assert.Contains(notices,n=>n.RecipientId==account.Approver && n.Type=="Overdue");
        Assert.Contains(notices,n=>n.RecipientId==account.Requester && n.Type=="Escalation");
        Assert.Equal(2,await db.AuditLogs.CountAsync(a=>a.ObjectId==id && a.Action=="WORKFLOW.DEADLINE_RAISED"));
        var foreign=await Seed();Assert.Equal(0,await first.SweepTenantAsync(foreign.Tenant,default));
    }

    [Fact]
    public async Task Sla_pause_is_authorized_audited_and_resume_shifts_the_deadline_without_allowing_decisions_while_paused()
    {
        var account=await Seed();await AddBackup(account);var clock=new MutableClock(DateTimeOffset.UtcNow);
        await using var factory=new AuthenticationFactory(database,configureServices:s=>s.AddSingleton<TimeProvider>(clock));
        using var owner=await Login(factory,account);using var approver=await Login(factory,account,true);using var outsider=await Login(factory,account,identifier:"BACKUP");
        using var created=await Create(owner,account);Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run=(await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;var step=run.Steps.Single();var due=step.DueAt!.Value;
        var path=$"/api/v1/requests/{id}/workflow/steps/{step.Id}/sla";
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.PostAsJsonAsync($"{path}/pause",new WorkflowSlaInput(run.Revision,"Không có quyền"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await approver.PostAsJsonAsync($"{path}/pause",new WorkflowSlaInput(run.Revision," "))).StatusCode);
        using var paused=await approver.PostAsJsonAsync($"{path}/pause",new WorkflowSlaInput(run.Revision,"Chờ chứng từ từ bộ phận mua hàng"));
        Assert.True(paused.IsSuccessStatusCode,await paused.Content.ReadAsStringAsync());run=(await paused.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        Assert.NotNull(run.Steps.Single().PausedAt);Assert.False(run.Steps.Single().CanAct);Assert.True(run.Steps.Single().CanManageSla);
        await using (var evidence = database.Create(account.Requester,account.Tenant))
        {
            var forged = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => evidence.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkflowSlaPause\" SET \"ResumedAt\"=\"PausedAt\", \"ResumedBy\"={account.Approver}, \"DueAtAfter\"=\"DueAtBefore\" WHERE \"StepExecutionId\"={step.Id}"));
            Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation,forged.SqlState);
        }
        Assert.Equal(HttpStatusCode.Conflict,(await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Chưa tiếp tục"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PostAsJsonAsync($"{path}/pause",new WorkflowSlaInput(run.Revision,"Đã tạm dừng"))).StatusCode);
        clock.Now=clock.Now.AddMinutes(10);
        using var resumed=await approver.PostAsJsonAsync($"{path}/resume",new WorkflowSlaInput(run.Revision));
        Assert.True(resumed.IsSuccessStatusCode,await resumed.Content.ReadAsStringAsync());run=(await resumed.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        var resumedStep=run.Steps.Single();Assert.Null(resumedStep.PausedAt);Assert.Equal(due.AddMinutes(10),resumedStep.DueAt);Assert.True(resumedStep.CanAct);
        Assert.Single(resumedStep.SlaPauses);Assert.NotNull(resumedStep.SlaPauses.Single().ResumedAt);
        using var decision=await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Đã bổ sung chứng từ"));
        Assert.True(decision.IsSuccessStatusCode,await decision.Content.ReadAsStringAsync());
        await using var db=database.Create(account.Requester,account.Tenant);
        var pause=await db.WorkflowSlaPauses.SingleAsync();
        var retained=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"WorkflowSlaPause\" WHERE \"Id\"={pause.Id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation,retained.SqlState);
        Assert.Equal(2,await db.AuditLogs.CountAsync(a=>a.ObjectId==id && (a.Action=="WORKFLOW.SLA_PAUSED" || a.Action=="WORKFLOW.SLA_RESUMED")));
    }

    [Theory]
    [InlineData(1000,"ANY",1)]
    [InlineData(1001,"ALL",2)]
    public async Task Approval_matrix_pins_the_correct_tier_candidates_and_policy(decimal budget,string policy,int count)
    {
        var account = await Seed(matrix:true);
        await using var factory = new AuthenticationFactory(database);
        using var owner = await Login(factory,account); using var approver = await Login(factory,account,true);
        using var validationResponse = await owner.PostAsJsonAsync($"/api/v1/workflow-versions/{account.Version}/validate",new{});
        Assert.True(validationResponse.IsSuccessStatusCode,await validationResponse.Content.ReadAsStringAsync());
        var validation = (await validationResponse.Content.ReadFromJsonAsync<WorkflowValidationResult>())!;
        Assert.True(validation.IsValid,string.Join("; ",validation.Errors));
        using var creation = await Create(owner,account,new{budget});
        Assert.True(creation.IsSuccessStatusCode,await creation.Content.ReadAsStringAsync());
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = (await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        var step = run.Steps.Single();
        Assert.Equal(policy,step.Policy); Assert.Equal(count,step.CandidateDetails.Count);Assert.True(step.CanAct);
        Assert.DoesNotContain(step.CandidateDetails,c=>c.UserId==account.Requester);
        using var decision = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Đúng hạn mức"));
        Assert.True(decision.IsSuccessStatusCode,await decision.Content.ReadAsStringAsync());
        var result=(await decision.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        Assert.Equal(policy=="ALL"?"PENDING":"COMPLETED",result.Steps.Single(s=>s.Id==step.Id).Status);
        Assert.Equal(budget,result.FormData.GetProperty("budget").GetDecimal());
    }

    [Fact]
    public async Task Delegation_migration_backfills_existing_decisions_without_losing_runtime_history()
    {
        var isolated = new PostgresFixture();
        try
        {
            await isolated.InitializeAsync();
            var account = await new WorkflowRuntimeTests(isolated).Seed();
            await using var factory = new AuthenticationFactory(isolated);
            using var owner = await Login(factory,account); using var approver = await Login(factory,account,true);
            using var created = await Create(owner,account); Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
            var run = (await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
            using var approval = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{run.Steps.Single().Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Bằng chứng trước nâng cấp"));
            Assert.True(approval.IsSuccessStatusCode,await approval.Content.ReadAsStringAsync());
            await using var migration = isolated.Create(null,null);
            await migration.GetService<IMigrator>().MigrateAsync("20261124174442_WorkflowRuntimeFoundation");
            await migration.Database.MigrateAsync();
            await using var verify = isolated.Create(account.Requester,account.Tenant);
            var decision = await verify.WorkflowDecisions.SingleAsync();
            Assert.Equal(account.Approver,decision.ActorId); Assert.Equal(decision.ActorId,decision.PrincipalId);
            Assert.Equal("Bằng chứng trước nâng cấp",decision.Note);
            Assert.Equal(2,await verify.WorkflowExecutions.CountAsync());
        }
        finally { await isolated.DisposeAsync(); }
    }

    [Fact]
    public async Task Delegation_unblocks_inactive_ALL_candidate_without_counting_a_person_twice_and_retains_evidence()
    {
        var account = await Seed("ALL"); var backupId = await AddBackup(account);
        await using var factory = new AuthenticationFactory(database);
        using var owner = await Login(factory, account); using var approver = await Login(factory, account, true);
        using var backup = await Login(factory, account, identifier:"BACKUP");
        using var creation = await Create(owner, account); Assert.True(creation.IsSuccessStatusCode, await creation.Content.ReadAsStringAsync());
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = (await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        var step = run.Steps.Single();
        using var first = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Kiểm tra thứ nhất"));
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        run = (await first.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        var absent = step.CandidateDetails.Single(c => c.UserId != account.Approver).UserId;
        await using var db = database.Create(account.Requester, account.Tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={absent}");
        var path = $"/api/v1/requests/{id}/workflow/steps/{step.Id}/delegations";
        var input = new WorkflowDelegationInput(run.Revision,absent,backupId,DateTimeOffset.UtcNow.AddDays(2),"Nghỉ phép; thay người phụ trách");
        Assert.Equal(HttpStatusCode.Forbidden,(await approver.PostAsJsonAsync(path,input)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await owner.PostAsJsonAsync(path,input with {DelegateId=account.Requester})).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await owner.PostAsJsonAsync(path,input with {DelegateId=account.Approver})).StatusCode);
        var attempts = await Task.WhenAll(owner.PostAsJsonAsync(path,input),owner.PostAsJsonAsync(path,input));
        Assert.Single(attempts,r=>r.IsSuccessStatusCode); Assert.Single(attempts,r=>r.StatusCode==HttpStatusCode.Conflict);
        foreach(var response in attempts) response.Dispose();
        var inbox = (await backup.GetFromJsonAsync<WorkflowInboxPage>("/api/v1/workflow-inbox"))!;
        Assert.Contains(inbox.Items,x=>x.RequestId==id);
        var delegated = (await backup.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.True(delegated.Steps.Single().CanAct); Assert.False(delegated.CanDelegate);
        using var decision = await backup.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(delegated.Revision,"APPROVE","Duyệt thay"));
        Assert.True(decision.IsSuccessStatusCode,await decision.Content.ReadAsStringAsync());
        var result = (await decision.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        var recorded = result.Steps.Single(s=>s.Id==step.Id).Decisions.Single(d=>d.ActorId==backupId);
        Assert.Equal(absent,recorded.PrincipalId); Assert.Equal(2,result.Steps.Count);
        var grant = await db.WorkflowDelegations.SingleAsync();
        var guard = await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"WorkflowDelegation\" WHERE \"Id\"={grant.Id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation,guard.SqlState);
        var immutable = await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowDelegation\" SET \"Reason\"='edited' WHERE \"Id\"={grant.Id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation,immutable.SqlState);
    }

    [Fact]
    public async Task Revocation_and_expiry_remove_delegate_authority_and_restore_the_original_slot()
    {
        var account = await Seed(); var backupId = await AddBackup(account);
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = new AuthenticationFactory(database,configureServices:s=>s.AddSingleton<TimeProvider>(clock));
        using var owner = await Login(factory, account); using var approver = await Login(factory, account, true);
        using var backup = await Login(factory, account, identifier:"BACKUP");
        using var creation = await Create(owner, account); Assert.True(creation.IsSuccessStatusCode,await creation.Content.ReadAsStringAsync());
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = (await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!; var step = run.Steps.Single();
        var path = $"/api/v1/requests/{id}/workflow/steps/{step.Id}/delegations";
        using var assigned = await owner.PostAsJsonAsync(path,new WorkflowDelegationInput(run.Revision,account.Approver,backupId,clock.Now.AddMinutes(1),"Vắng mặt tạm thời"));
        Assert.True(assigned.IsSuccessStatusCode,await assigned.Content.ReadAsStringAsync());
        run = (await assigned.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        Assert.DoesNotContain((await approver.GetFromJsonAsync<WorkflowInboxPage>("/api/v1/workflow-inbox"))!.Items,x=>x.RequestId==id);
        Assert.Equal(HttpStatusCode.NotFound,(await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Không được xử lý khi đã giao thay"))).StatusCode);
        var grant = run.Steps.Single().Delegations.Single();
        var other = await Seed(); using var foreign = await Login(factory,other);
        Assert.Equal(HttpStatusCode.NotFound,(await foreign.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/delegations/{grant.Id}/revoke",new {revision=run.Revision})).StatusCode);
        clock.Now=clock.Now.AddMinutes(2);
        Assert.DoesNotContain((await backup.GetFromJsonAsync<WorkflowInboxPage>("/api/v1/workflow-inbox"))!.Items,x=>x.RequestId==id);
        Assert.False((await backup.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!.Steps.Single().CanAct);
        Assert.Equal(HttpStatusCode.NotFound,(await backup.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Đã hết hạn"))).StatusCode);
        using var again = await owner.PostAsJsonAsync(path,new WorkflowDelegationInput(run.Revision,account.Approver,backupId,clock.Now.AddDays(1),"Gia hạn có phê duyệt mới"));
        Assert.True(again.IsSuccessStatusCode,await again.Content.ReadAsStringAsync()); run=(await again.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        var active = run.Steps.Single().Delegations.Single(d=>d.Active);
        using var revoked = await owner.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/delegations/{active.Id}/revoke",new {revision=run.Revision});
        Assert.True(revoked.IsSuccessStatusCode,await revoked.Content.ReadAsStringAsync()); run=(await revoked.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        Assert.All(run.Steps.Single().Delegations,d=>Assert.False(d.Active));
        Assert.Contains((await approver.GetFromJsonAsync<WorkflowInboxPage>("/api/v1/workflow-inbox"))!.Items,x=>x.RequestId==id);
        using var original = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Người gốc xử lý"));
        Assert.True(original.IsSuccessStatusCode,await original.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Submission_starts_pinned_workflow_and_two_different_people_finish_it_after_reload()
    {
        var account = await Seed(); await using var factory = new AuthenticationFactory(database);
        using var requester = await Login(factory, account); using var approver = await Login(factory, account, true);
        using var creation = await Create(requester, account); Assert.True(creation.IsSuccessStatusCode, await creation.Content.ReadAsStringAsync());
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = await requester.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow");
        Assert.NotNull(run); Assert.Equal(account.Version, run.VersionId); Assert.Single(run.Steps); Assert.False(run.Steps[0].CanAct);
        var inbox = await approver.GetFromJsonAsync<WorkflowInboxPage>("/api/v1/workflow-inbox"); Assert.Contains(inbox!.Items, x => x.RequestId == id);
        var metrics = await requester.GetFromJsonAsync<WorkflowMetrics>("/api/v1/workflow-metrics?days=30");
        Assert.Equal(1, metrics!.Steps.Sum(s => s.Waiting));
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync("/api/v1/workflow-metrics?days=30")).StatusCode);
        using var forbidden = await requester.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{run.Steps[0].Id}/decisions", new WorkflowDecisionInput(run.Revision, "APPROVE", "Không được tự duyệt"));
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var approval = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{run.Steps[0].Id}/decisions", new WorkflowDecisionInput(run.Revision, "APPROVE", "Đồng ý"));
        Assert.True(approval.IsSuccessStatusCode, await approval.Content.ReadAsStringAsync());
        var reloaded = await requester.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow");
        Assert.Equal(2, reloaded!.Steps.Count); Assert.True(reloaded.Steps[1].CanAct);
        using var complete = await requester.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{reloaded.Steps[1].Id}/decisions", new WorkflowDecisionInput(reloaded.Revision, "APPROVE"));
        Assert.True(complete.IsSuccessStatusCode, await complete.Content.ReadAsStringAsync());
        await using var db = database.Create(account.Requester, account.Tenant);
        Assert.Equal(RequestState.Closed, (await db.Requests.SingleAsync(x => x.Id == id)).Status);
        var finishedMetrics = await requester.GetFromJsonAsync<WorkflowMetrics>("/api/v1/workflow-metrics?days=30");
        Assert.Equal(0, finishedMetrics!.Steps.Sum(s => s.Waiting));
        Assert.Equal(2, finishedMetrics.Steps.Sum(s => s.Completed));
        Assert.All(finishedMetrics.Steps, s => Assert.True(s.P90ActualMinutes >= 0));
        Assert.Equal("COMPLETED", (await db.WorkflowInstances.SingleAsync()).Status);
        Assert.Equal(2, await db.WorkflowDecisions.CountAsync());
        var decisionId = await db.WorkflowDecisions.Select(d => d.Id).FirstAsync();
        var retained = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"WorkflowDecision\" WHERE \"Id\"={decisionId}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation, retained.SqlState);
        var immutable = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"WorkflowInstance\" SET \"PayloadJson\"='{{}}'::jsonb WHERE \"RequestId\"={id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation, immutable.SqlState);
        Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.Action.StartsWith("WORKFLOW.") && a.ObjectId == id));
        using var replay = await requester.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{reloaded.Steps[1].Id}/decisions", new WorkflowDecisionInput(reloaded.Revision, "APPROVE"));
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task Required_form_and_self_approval_fail_without_leaving_half_created_requests()
    {
        var account = await Seed(selfApproval:true); await using var factory = new AuthenticationFactory(database); using var client = await Login(factory, account);
        using var missing = await Create(client, account, new { }); Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        using var self = await Create(client, account); Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        await using var db = database.Create(account.Requester, account.Tenant);
        Assert.Empty(await db.Requests.ToListAsync()); Assert.Empty(await db.WorkflowInstances.ToListAsync());
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
    }

    [Fact]
    public async Task Other_tenant_cannot_read_or_decide_and_rejection_is_terminal()
    {
        var account = await Seed(); var other = await Seed(); await using var factory = new AuthenticationFactory(database);
        using var owner = await Login(factory, account); using var stranger = await Login(factory, other); using var approver = await Login(factory, account, true);
        using var created = await Create(owner, account); Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = (await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        using var read = await stranger.GetAsync($"/api/v1/requests/{id}/workflow"); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var write = await stranger.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{run.Steps[0].Id}/decisions", new WorkflowDecisionInput(run.Revision,"REJECT","Không hợp lệ"));
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        using var reject = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{run.Steps[0].Id}/decisions", new WorkflowDecisionInput(run.Revision,"REJECT","Thiếu căn cứ"));
        Assert.True(reject.IsSuccessStatusCode, await reject.Content.ReadAsStringAsync());
        var result = await reject.Content.ReadFromJsonAsync<WorkflowRuntimeView>(); Assert.Equal("REJECTED", result!.Status);
        owner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var revision = await owner.PostAsJsonAsync($"/api/v1/requests/{id}/revise", new {submitImmediately=true});
        Assert.True(revision.IsSuccessStatusCode, await revision.Content.ReadAsStringAsync());
        var receipt = await revision.Content.ReadFromJsonAsync<JsonElement>();
        using var replay = await owner.PostAsJsonAsync($"/api/v1/requests/{id}/revise", new {submitImmediately=true});
        Assert.True(replay.IsSuccessStatusCode, await replay.Content.ReadAsStringAsync());
        Assert.Equal(receipt.GetRawText(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetRawText());
        var revisedRun = await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{receipt.GetProperty("requestId").GetGuid()}/workflow");
        Assert.Equal("RUNNING", revisedRun!.Status);
        Assert.Equal(1200, revisedRun.FormData.GetProperty("budget").GetInt32());
    }

    [Fact]
    public async Task All_policy_waits_for_every_candidate_and_concurrent_retries_advance_only_once()
    {
        var account = await Seed("ALL"); await using var factory = new AuthenticationFactory(database);
        using var owner = await Login(factory, account); using var first = await Login(factory, account, true); using var second = await Login(factory, account, identifier:"SECOND");
        using var creation = await Create(owner, account); Assert.True(creation.IsSuccessStatusCode, await creation.Content.ReadAsStringAsync());
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = (await first.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        var path = $"/api/v1/requests/{id}/workflow/steps/{run.Steps[0].Id}/decisions";
        var responses = await Task.WhenAll(first.PostAsJsonAsync(path, new WorkflowDecisionInput(run.Revision,"APPROVE","Duyệt lần một")),
            first.PostAsJsonAsync(path, new WorkflowDecisionInput(run.Revision,"APPROVE","Duyệt lần một")));
        Assert.Single(responses, r => r.IsSuccessStatusCode); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        var waiting = (await second.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.Single(waiting.Steps); Assert.Equal("PENDING", waiting.Steps[0].Status); Assert.Single(waiting.Steps[0].Decisions);
        using var last = await second.PostAsJsonAsync(path, new WorkflowDecisionInput(waiting.Revision,"APPROVE","Duyệt lần hai"));
        Assert.True(last.IsSuccessStatusCode, await last.Content.ReadAsStringAsync());
        var advanced = await last.Content.ReadFromJsonAsync<WorkflowRuntimeView>(); Assert.Equal(2, advanced!.Steps.Count);
        Assert.Equal(2, advanced.Steps[0].Decisions.Count);
    }

    [Fact]
    public async Task Parallel_branches_join_only_after_both_people_finish_and_survive_reload()
    {
        var account = await Seed(parallel:true); await using var factory = new AuthenticationFactory(database);
        using var owner = await Login(factory, account); using var approver = await Login(factory, account, true); using var second = await Login(factory, account, identifier:"SECOND");
        using var created = await Create(owner, account); Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run = (await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.Equal(2, run.Steps.Count(s => s.Status == "PENDING"));
        var approval = run.Steps.Single(s => s.NodeId == "approve");
        using var first = await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{approval.Id}/decisions",new WorkflowDecisionInput(run.Revision,"APPROVE","Đã duyệt"));
        Assert.True(first.IsSuccessStatusCode,await first.Content.ReadAsStringAsync());
        var halfway = (await second.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.DoesNotContain(halfway.Steps,s => s.NodeId == "confirm");
        Assert.Equal("PENDING",halfway.Steps.Single(s => s.NodeId == "join").Status);
        var equipment = halfway.Steps.Single(s => s.NodeId == "equipment");
        using var last = await second.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{equipment.Id}/decisions",new WorkflowDecisionInput(halfway.Revision,"COMPLETE","Đã bàn giao"));
        Assert.True(last.IsSuccessStatusCode,await last.Content.ReadAsStringAsync());
        var joined = await last.Content.ReadFromJsonAsync<WorkflowRuntimeView>();
        Assert.Equal("COMPLETED",joined!.Steps.Single(s => s.NodeId == "join").Status);
        Assert.Single(joined.Steps,s => s.NodeId == "confirm");
    }
}
