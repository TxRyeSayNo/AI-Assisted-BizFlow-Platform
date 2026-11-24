using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Workflows;

public sealed class WorkflowAgentDispatcher(DbContextOptions<BizFlowDbContext> options, IServiceScopeFactory scopes,
    IConfiguration configuration, TimeProvider clock, ILogger<WorkflowAgentDispatcher> logger)
{
    // Non-user system identity only enables tenant-scoped filters; no user decision is recorded.
    private sealed record AgentContext(Guid? TenantId) : IWorkflowAgentContext
    {
        public Guid? UserId => TenantId is null ? null : new Guid("00000000-0000-0000-0000-000000000001");
    }
    public bool Enabled => configuration.GetValue("AI:WorkflowAgent:Enabled", true);

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        if (!Enabled) return 0;
        var scanTime = clock.GetUtcNow();
        await using var scan = new BizFlowDbContext(options, new AgentContext(null));
        var tenants = await scan.Database.SqlQuery<Guid>($"""
            SELECT a."TenantId" AS "Value" FROM "WorkflowAgentRun" a
            JOIN "Tenant" t ON t."TenantId"=a."TenantId" JOIN "Company" c ON c."CompanyId"=t."CompanyId"
            JOIN "WorkflowStepExecution" s ON s."Id"=a."StepId" AND s."TenantId"=a."TenantId"
            WHERE t."Status"='ACTIVE' AND c."Status"='ACTIVE' AND s."Status"='PENDING' AND s."PausedAt" IS NULL
              AND ((a."Status" IN ('QUEUED','RETRY') AND a."NextAttemptAt"<={scanTime}) OR (a."Status"='RUNNING' AND a."LeaseUntil"<={scanTime}))
            GROUP BY a."TenantId" ORDER BY MIN(a."NextAttemptAt"),a."TenantId" LIMIT 100
            """).ToArrayAsync(ct);
        var processed = 0;
        var parallelism = Math.Clamp(configuration.GetValue("AI:WorkflowAgent:MaxConcurrency", 2), 1, 8);
        await Parallel.ForEachAsync(tenants, new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct }, async (tenant, token) =>
        {
            try { Interlocked.Add(ref processed, await SweepTenantAsync(tenant, token)); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogWarning("Workflow AI queue failed for tenant {TenantId}; it will be retried.", tenant); }
        });
        return processed;
    }

    public async Task<int> SweepTenantAsync(Guid tenant, CancellationToken ct)
    {
        if (!Enabled) return 0;
        var now = clock.GetUtcNow(); var lease = Guid.NewGuid(); var until = now.AddMinutes(3);
        await using var claimDb = new BizFlowDbContext(options, new AgentContext(tenant));
        // Recover an abandoned last attempt without letting a crashed process consume infinite budget.
        await claimDb.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkflowAgentRun" SET "Status"='FAILED',"ErrorCode"='AI.LEASE_EXHAUSTED',"CompletedAt"={now},"LeaseToken"=NULL,"LeaseUntil"=NULL
            WHERE "TenantId"={tenant} AND "Status"='RUNNING' AND "Attempts">=9 AND "LeaseUntil"<={now};
            UPDATE "WorkflowAgentRun" a SET "Status"='SUPERSEDED',"CompletedAt"={now},"LeaseToken"=NULL,"LeaseUntil"=NULL
            WHERE a."TenantId"={tenant} AND a."Status" IN ('QUEUED','RETRY','RUNNING') AND EXISTS (
              SELECT 1 FROM "WorkflowStepExecution" s JOIN "WorkflowInstance" i ON i."Id"=s."InstanceId"
              JOIN "Request" r ON r."RequestId"=i."RequestId"
              WHERE s."Id"=a."StepId" AND (s."Status"<>'PENDING' OR i."Status"<>'RUNNING' OR r."DeletedAt" IS NOT NULL));
            """, ct);
        var claimed = await claimDb.Database.SqlQuery<WorkflowAgentRow>($"""
            WITH candidate AS (
              SELECT a."Id" FROM "WorkflowAgentRun" a
              JOIN "WorkflowStepExecution" s ON s."Id"=a."StepId" AND s."TenantId"=a."TenantId"
              JOIN "WorkflowInstance" i ON i."Id"=a."InstanceId" AND i."TenantId"=a."TenantId"
              JOIN "Tenant" t ON t."TenantId"=a."TenantId" JOIN "Company" c ON c."CompanyId"=t."CompanyId"
              WHERE a."TenantId"={tenant} AND t."Status"='ACTIVE' AND c."Status"='ACTIVE'
                AND i."Status"='RUNNING' AND s."Status"='PENDING' AND s."PausedAt" IS NULL AND a."Attempts"<9
                AND ((a."Status" IN ('QUEUED','RETRY') AND a."NextAttemptAt"<={now}) OR (a."Status"='RUNNING' AND a."LeaseUntil"<={now}))
              ORDER BY a."NextAttemptAt",a."Id" LIMIT 1 FOR UPDATE OF a SKIP LOCKED
            )
            UPDATE "WorkflowAgentRun" a SET "Status"='RUNNING',"Attempts"=a."Attempts"+1,"LeaseToken"={lease},"LeaseUntil"={until}
            FROM candidate WHERE a."Id"=candidate."Id" RETURNING a.*
            """).ToArrayAsync(ct);
        if (claimed.Length == 0) return 0;
        var job = claimed[0];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        await using var scope = scopes.CreateAsyncScope();
        var reasoner = scope.ServiceProvider.GetRequiredService<WorkflowAgentReasoner>();
        WorkflowAgentEvaluation? evaluation = null; string? error = null;
        try { evaluation = await reasoner.EvaluateAsync(job, timeout.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { error = "AI.TIMEOUT"; }
        catch (ApplicationFault failure) { error = failure.Code.Length <= 100 ? failure.Code : "AI.UNAVAILABLE"; }
        catch (System.Text.Json.JsonException) { error = "AI.INVALID_OUTPUT"; }
        catch (Exception e) when (e is not OperationCanceledException) { error = "AI.UNAVAILABLE"; }
        async Task Finish(string? failure)
        {
            await using var finishDb = new BizFlowDbContext(options, new AgentContext(tenant));
            var store = ActivatorUtilities.CreateInstance<WorkflowRuntimeStore>(scope.ServiceProvider, finishDb, new AgentContext(tenant));
            await store.FinishAgentAsync(job, evaluation?.Result, evaluation?.TraceJson ?? "[]", failure, evaluation?.Model, now, ct);
        }
        try { await Finish(error); }
        catch (ApplicationFault failure)
        {
            logger.LogWarning("Workflow AI completion was blocked by {Code}; handing the step to a person.", failure.Code);
            await Finish("AI.PROGRESSION_BLOCKED");
        }
        return 1;
    }
}

public sealed class WorkflowAgentWorker(IServiceScopeFactory scopes, ILogger<WorkflowAgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<WorkflowAgentDispatcher>().SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch { logger.LogWarning("Workflow AI queue could not be processed; it will be retried."); }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
