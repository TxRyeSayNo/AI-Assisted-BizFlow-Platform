using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.AI;
using BizFlow.Application.Workflows;
using BizFlow.Infrastructure.Workflows;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BizFlow.IntegrationTests.Workflows;

public sealed partial class WorkflowRuntimeTests
{
    private sealed class WorkflowModel : IAiModelClient
    {
        public bool IsConfigured { get; set; } = true;
        public int Calls;
        public bool Invalid;
        public bool NeedsHuman;
        public Func<Task>? BeforeResult;
        public async Task<AiModelResponse> CompleteAsync(string prompt, string systemInstructions, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            using var doc = JsonDocument.Parse(prompt);
            if (doc.RootElement.GetProperty("observations").GetArrayLength() == 0) return new("""{"tool":"read_inputs"}""","test-workflow-model",1,10,10);
            if (BeforeResult is not null) await BeforeResult();
            return new(JsonSerializer.Serialize(new { result = new WorkflowAgentResult("Dự toán cần duyệt", "Dự toán 1200. Chuyển người có thẩm quyền quyết định.", NeedsHuman,
                ["Chưa có bằng chứng ngân sách thực tế."], [new(Invalid?"fields.secret":"fields.budget",Invalid?"invented":"1200")]) },WorkflowAgentContract.Json),"test-workflow-model",2,30,20);
        }
    }
    private AuthenticationFactory AgentFactory(WorkflowModel model, MutableClock? clock = null, bool enabled = true) => new(database,new Dictionary<string,string?>{{"AI:WorkflowAgent:Enabled",enabled.ToString()}},configureServices: services =>
    {
        foreach (var item in services.Where(s=>s.ServiceType==typeof(IHostedService) && (s.ImplementationType==typeof(WorkflowAgentWorker)||s.ImplementationType==typeof(BizFlow.Infrastructure.AI.ProactiveAgentDaemon))).ToArray()) services.Remove(item);
        services.AddSingleton<IAiModelClient>(model);
        if (clock is not null) services.AddSingleton<TimeProvider>(clock);
    });
    private static async Task<int> SweepAgent(AuthenticationFactory factory,Guid tenant)
    {
        using var scope=factory.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<WorkflowAgentDispatcher>().SweepTenantAsync(tenant,default);
    }

    [Theory]
    [InlineData("AUTO",false,"COMPLETED",2)]
    [InlineData("REVIEW",false,"REVIEW_REQUIRED",1)]
    [InlineData("AUTO",true,"REVIEW_REQUIRED",1)]
    public async Task Workflow_agent_runs_a_bounded_tool_loop_and_keeps_approval_human(string mode,bool needsHuman,string expected,int stepCount)
    {
        var account=await Seed(agentMode:mode);var model=new WorkflowModel{NeedsHuman=needsHuman};await using var factory=AgentFactory(model);
        using var owner=await Login(factory,account);using var created=await Create(owner,account);Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var concurrent=await Task.WhenAll(SweepAgent(factory,account.Tenant),SweepAgent(factory,account.Tenant));Assert.Equal(1,concurrent.Sum());Assert.Equal(2,model.Calls);
        var run=(await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.Equal(stepCount,run.Steps.Count);var ai=Assert.Single(run.Steps,s=>s.NodeId=="agent");Assert.Equal(expected,ai.Agent!.Status);Assert.Contains("1200",ai.Agent.Result!.Deliverable);
        Assert.Empty(ai.Decisions);Assert.Equal("RUNNING",run.Status);
        if(mode=="AUTO"&&!needsHuman)Assert.Equal("PENDING",Assert.Single(run.Steps,s=>s.NodeType=="APPROVAL").Status);
        Assert.Equal(0,await SweepAgent(factory,account.Tenant));
        var history=(await owner.GetFromJsonAsync<WorkflowAgentAttemptView[]>($"/api/v1/requests/{id}/workflow/steps/{ai.Id}/agent/history"))!;
        Assert.Single(history);Assert.Contains(history[0].Trace.EnumerateArray(),e=>e.TryGetProperty("result",out _));
        await using var db=database.Create(account.Requester,account.Tenant);
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.ObjectId==id&&a.Action=="WORKFLOW.AGENT_FINISHED"&&a.ActorType==BizFlow.Domain.Audit.AuditActorType.AiAgent));
        db.AuditLogs.Add(BizFlow.Domain.Audit.AuditLog.WorkflowAgentChanged(account.Tenant,id,"WORKFLOW.AGENT_FINISHED",new{},DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<BizFlow.Application.Common.ApplicationFault>(()=>db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var attempts=await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"WorkflowAgentAttempt\" WHERE \"TenantId\"={account.Tenant}").SingleAsync();Assert.Equal(1,attempts);
        var foreign=await Seed();using var outsider=await Login(factory,foreign);Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/api/v1/requests/{id}/workflow")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/api/v1/requests/{id}/workflow/steps/{ai.Id}/agent/history")).StatusCode);
    }

    [Fact]
    public async Task Agent_retries_invalid_evidence_with_backoff_then_hands_over_and_retains_immutable_attempts()
    {
        var account=await Seed(agentMode:"AUTO");var model=new WorkflowModel{Invalid=true};var clock=new MutableClock(DateTimeOffset.UtcNow);
        await using var factory=AgentFactory(model,clock);using var owner=await Login(factory,account);using var created=await Create(owner,account);
        Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        Assert.Equal(1,await SweepAgent(factory,account.Tenant));Assert.Equal(0,await SweepAgent(factory,account.Tenant));
        clock.Now=clock.Now.AddMinutes(2);Assert.Equal(1,await SweepAgent(factory,account.Tenant));clock.Now=clock.Now.AddMinutes(6);Assert.Equal(1,await SweepAgent(factory,account.Tenant));
        // Avoid issuing future JWTs; the owner token predates clock changes.
        var run=(await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;var step=Assert.Single(run.Steps);
        Assert.Equal("FAILED",step.Agent!.Status);Assert.Equal(3,step.Agent.Attempts);Assert.Null(step.Agent.Result);Assert.Equal("PENDING",step.Status);
        await using var db=database.Create(account.Requester,account.Tenant);
        var attemptCount=await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"WorkflowAgentAttempt\" WHERE \"RunId\"={step.Agent.Id}").SingleAsync();Assert.Equal(3,attemptCount);
        var guarded=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowAgentRun\" SET \"Instructions\"='changed' WHERE \"Id\"={step.Agent.Id}"));Assert.Equal("23514",guarded.SqlState);
        var appendOnly=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"WorkflowAgentAttempt\" WHERE \"RunId\"={step.Agent.Id}"));Assert.Equal("23514",appendOnly.SqlState);
        model.Invalid=false;
        using var retry=await owner.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/agent/retry",new WorkflowAgentRetryInput(run.Revision,"Kiểm tra lại sau khi dịch vụ phục hồi"));Assert.True(retry.IsSuccessStatusCode,await retry.Content.ReadAsStringAsync());
        using var duplicate=await owner.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/agent/retry",new WorkflowAgentRetryInput(run.Revision,"Không chạy trùng"));Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        Assert.Equal(1,await SweepAgent(factory,account.Tenant));
        var completed=(await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;Assert.Equal("COMPLETED",completed.Steps.Single(s=>s.Id==step.Id).Agent!.Status);
    }

    [Fact]
    public async Task Human_completion_while_model_runs_prevents_late_AI_application_and_paused_steps_do_not_call_model()
    {
        var account=await Seed(agentMode:"AUTO");var model=new WorkflowModel();await using var factory=AgentFactory(model);
        using var owner=await Login(factory,account);using var approver=await Login(factory,account,true);using var created=await Create(owner,account);
        Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync());var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        var run=(await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;var step=Assert.Single(run.Steps);
        using var paused=await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/sla/pause",new WorkflowSlaInput(run.Revision,"Chờ xác minh"));Assert.True(paused.IsSuccessStatusCode);
        Assert.Equal(0,await SweepAgent(factory,account.Tenant));Assert.Equal(0,model.Calls);
        var pausedRun=(await paused.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        using var resumed=await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/sla/resume",new WorkflowSlaInput(pausedRun.Revision));Assert.True(resumed.IsSuccessStatusCode);
        var resumedRun=(await resumed.Content.ReadFromJsonAsync<WorkflowRuntimeView>())!;
        model.BeforeResult=async()=>{using var human=await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(resumedRun.Revision,"COMPLETE","Đã xử lý thủ công"));Assert.True(human.IsSuccessStatusCode,await human.Content.ReadAsStringAsync());};
        Assert.Equal(1,await SweepAgent(factory,account.Tenant));
        var final=(await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.Equal("HUMAN_COMPLETED",final.Steps.Single(s=>s.Id==step.Id).Agent!.Status);Assert.Single(final.Steps.Single(s=>s.Id==step.Id).Decisions);Assert.Equal(2,final.Steps.Count);
    }

    [Fact]
    public async Task Unconfigured_model_hands_over_without_fabricating_a_result()
    {
        var account=await Seed(agentMode:"AUTO");var model=new WorkflowModel{IsConfigured=false};await using var factory=AgentFactory(model);
        using var owner=await Login(factory,account);using var created=await Create(owner,account);Assert.True(created.IsSuccessStatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();Assert.Equal(1,await SweepAgent(factory,account.Tenant));
        var run=(await owner.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        Assert.Equal("REVIEW_REQUIRED",run.Steps.Single().Agent!.Status);Assert.Equal("AI.NOT_CONFIGURED",run.Steps.Single().Agent!.ErrorCode);Assert.Equal(0,model.Calls);
    }

    [Fact]
    public async Task Expired_worker_lease_is_recovered_and_pinned_input_excludes_unselected_data()
    {
        var account=await Seed(agentMode:"AUTO");var model=new WorkflowModel();await using var factory=AgentFactory(model);
        using var owner=await Login(factory,account);using var created=await Create(owner,account);Assert.True(created.IsSuccessStatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        await using var db=database.Create(account.Requester,account.Tenant);
        var jobs=await WorkflowAgentPersistence.ReadAsync(db,account.Tenant,(await db.WorkflowInstances.SingleAsync(i=>i.RequestId==id)).Id,default);var job=Assert.Single(jobs);
        Assert.Contains("budget",job.InputJson);Assert.DoesNotContain("description",job.InputJson);Assert.DoesNotContain(account.Key,job.InputJson);
        var expired=DateTimeOffset.UtcNow.AddMinutes(-1);var oldLease=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowAgentRun\" SET \"Status\"='RUNNING',\"Attempts\"=1,\"LeaseUntil\"={expired},\"LeaseToken\"={oldLease} WHERE \"Id\"={job.Id}");
        Assert.Equal(1,await SweepAgent(factory,account.Tenant));
        var recovered=Assert.Single(await WorkflowAgentPersistence.ReadAsync(db,account.Tenant,job.InstanceId,default));
        Assert.Equal(2,recovered.Attempts);Assert.Equal("COMPLETED",recovered.Status);
    }

    [Fact]
    public async Task Proactive_daemon_really_scans_a_pinned_tenant_without_HTTP_claims_and_deduplicates_replicas()
    {
        var account=await Seed();var foreign=await Seed();await using var factory=AgentFactory(new WorkflowModel());_ = factory.CreateClient();
        await using var db=database.Create(account.Requester,account.Tenant);
        var task=BizFlow.Domain.Tasks.WorkTask.CreateDraft(account.Tenant,account.Requester,new string('x',200),DateTimeOffset.UtcNow,deadline:DateTimeOffset.UtcNow.AddHours(1));
        db.WorkTasks.Add(task);await db.SaveChangesAsync();
        var daemon=new BizFlow.Infrastructure.AI.ProactiveAgentDaemon(factory.Services.GetRequiredService<IServiceScopeFactory>(),factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),Microsoft.Extensions.Logging.Abstractions.NullLogger<BizFlow.Infrastructure.AI.ProactiveAgentDaemon>.Instance);
        var results=await Task.WhenAll(daemon.RunTenantScanAsync(account.Tenant,default),daemon.RunTenantScanAsync(account.Tenant,default));Assert.Equal(1,results.Sum(r=>r.RecommendationsCreated));
        Assert.Equal(0,(await daemon.RunTenantScanAsync(account.Tenant,default)).RecommendationsCreated);
        Assert.Single(await db.AIRecommendations.ToArrayAsync());Assert.Single(await db.Notifications.ToArrayAsync());
        await using var other=database.Create(foreign.Requester,foreign.Tenant);Assert.Empty(await other.AIRecommendations.ToArrayAsync());Assert.Empty(await other.Notifications.ToArrayAsync());
    }

    [Fact]
    public async Task Worker_selects_due_tenants_and_processes_isolated_jobs_with_bounded_parallelism()
    {
        var a=await Seed(agentMode:"AUTO");var b=await Seed(agentMode:"REVIEW");var model=new WorkflowModel();await using var factory=AgentFactory(model);
        using var first=await Login(factory,a);using var second=await Login(factory,b);
        using var ca=await Create(first,a);using var cb=await Create(second,b);Assert.True(ca.IsSuccessStatusCode);Assert.True(cb.IsSuccessStatusCode);
        using var scope=factory.Services.CreateScope();var dispatcher=scope.ServiceProvider.GetRequiredService<WorkflowAgentDispatcher>();
        Assert.Equal(2,await dispatcher.SweepAsync(default));Assert.Equal(4,model.Calls);Assert.Equal(0,await dispatcher.SweepAsync(default));
    }

    [Fact]
    public async Task Kill_switch_prevents_model_calls_and_keeps_manual_processing_available()
    {
        var a=await Seed(agentMode:"AUTO");var model=new WorkflowModel();await using var factory=AgentFactory(model,enabled:false);
        using var owner=await Login(factory,a);using var created=await Create(owner,a);Assert.True(created.IsSuccessStatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        using var scope=factory.Services.CreateScope();Assert.Equal(0,await scope.ServiceProvider.GetRequiredService<WorkflowAgentDispatcher>().SweepAsync(default));Assert.Equal(0,model.Calls);
        using var approver=await Login(factory,a,true);var run=(await approver.GetFromJsonAsync<WorkflowRuntimeView>($"/api/v1/requests/{id}/workflow"))!;
        var step=Assert.Single(run.Steps);Assert.Equal("QUEUED",step.Agent!.Status);Assert.True(step.CanAct);
        using var manual=await approver.PostAsJsonAsync($"/api/v1/requests/{id}/workflow/steps/{step.Id}/decisions",new WorkflowDecisionInput(run.Revision,"COMPLETE","Xử lý thủ công khi AI tắt"));Assert.True(manual.IsSuccessStatusCode);
    }
}
