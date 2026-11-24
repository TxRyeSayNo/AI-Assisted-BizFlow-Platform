using System.Text.Json;
using System.Text.Json.Nodes;
using BizFlow.Application.Common;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Workflows;

public sealed partial class WorkflowRuntimeStore
{
    public async Task<IReadOnlyList<WorkflowAgentAttemptView>> AgentHistoryAsync(Guid requestId, Guid stepId, CancellationToken ct)
    {
        var view = await GetAsync(requestId, ct) ?? throw ApplicationFault.NotFound();
        var agent = view.Steps.SingleOrDefault(s => s.Id == stepId)?.Agent ?? throw ApplicationFault.NotFound();
        return await db.Database.SqlQuery<WorkflowAgentAttemptView>($"""
            SELECT a."Attempt",a."StartedAt",a."CompletedAt",a."Model",a."ErrorCode",a."TraceJson" AS "Trace"
            FROM "WorkflowAgentAttempt" a JOIN "WorkflowAgentRun" r ON r."Id"=a."RunId" AND r."TenantId"=a."TenantId"
            WHERE r."Id"={agent.Id} AND r."TenantId"={context.TenantId} AND r."RequestId"={requestId} AND r."StepId"={stepId}
            ORDER BY a."Attempt"
            """).ToArrayAsync(ct);
    }
    private async Task EnqueueAgents(WorkflowInstance instance, WorkRequest request, JsonElement definition, DateTimeOffset now, CancellationToken ct)
    {
        var steps = await db.WorkflowExecutions.Where(s => s.InstanceId == instance.Id && s.Status == "PENDING" && s.NodeType == "DEPARTMENT").ToArrayAsync(ct);
        var existing = (await WorkflowAgentPersistence.ReadAsync(db, instance.TenantId, instance.Id, ct)).Select(r => r.StepId).ToHashSet();
        using var payload = JsonDocument.Parse(instance.PayloadJson);
        foreach (var step in steps.Where(s => !existing.Contains(s.Id)))
        {
            var node = definition.GetProperty("nodes").EnumerateArray().Single(n => n.GetProperty("id").GetString() == step.NodeId);
            if (WorkflowAgentContract.Policy(node) is not { } policy) continue;
            var fields = new JsonObject();
            foreach (var key in policy.InputFields)
                fields[key] = payload.RootElement.TryGetProperty(key, out var v) ? JsonNode.Parse(v.GetRawText()) : null;
            var input = new JsonObject { ["title"] = payload.RootElement.GetProperty("title").GetString(), ["fields"] = fields,
                ["step"] = step.Name,
                ["requiredFields"] = JsonSerializer.SerializeToNode(WorkflowForm.Fields(definition).Where(f => f.Required && policy.InputFields.Contains(f.Key)).Select(f => f.Key)) };
            if (policy.IncludeDescription) input["description"] = request.Description;
            if (policy.IncludePreviousResults)
                input["previousResults"] = JsonSerializer.SerializeToNode((await WorkflowAgentPersistence.ReadAsync(db, instance.TenantId, instance.Id, ct))
                    .Where(r => r.Status == "COMPLETED" && r.ResultJson is not null).OrderByDescending(r => r.CompletedAt).Take(5)
                    .Select(r => new { stepId = r.StepId, summary = r.View().Result!.Summary }));
            var json = input.ToJsonString();
            if (json.Length > 32000) throw Invalid("Dữ liệu cho một bước AI tối đa 32000 ký tự; hãy giảm trường được đọc.");
            var id = Guid.CreateVersion7(); var hash = WorkflowAgentPersistence.Hash(json);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WorkflowAgentRun" ("Id","TenantId","InstanceId","RequestId","StepId","Mode","Instructions","InputJson","InputHash","CreatedAt","NextAttemptAt")
                VALUES ({id},{instance.TenantId},{instance.Id},{request.Id},{step.Id},{policy.Mode},{policy.Instructions},CAST({json} AS jsonb),{hash},{now},{now})
                ON CONFLICT ("StepId") DO NOTHING
                """, ct);
            if (existing.Count >= 20)
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowAgentRun\" SET \"Status\"='REVIEW_REQUIRED',\"ErrorCode\"='AI.RUN_BUDGET_EXCEEDED',\"CompletedAt\"={now} WHERE \"Id\"={id} AND \"TenantId\"={instance.TenantId}", ct);
            existing.Add(step.Id);
        }
    }

    public async Task<WorkflowRuntimeView> RetryAgentAsync(Guid requestId, Guid stepId, WorkflowAgentRetryInput input, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000 || input.Reason.Any(char.IsControl)) throw Invalid("Cần lý do chạy lại AI, tối đa 1000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await Lock(requestId, tenant, ct);
        var instance = await db.WorkflowInstances.SingleOrDefaultAsync(i => i.RequestId == requestId, ct) ?? throw ApplicationFault.NotFound();
        var step = await db.WorkflowExecutions.SingleOrDefaultAsync(s => s.Id == stepId && s.InstanceId == instance.Id, ct) ?? throw ApplicationFault.NotFound();
        var version = await db.WorkflowVersions.SingleAsync(v => v.Id == instance.WorkflowVersionId, ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        var node = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(n => n.GetProperty("id").GetString() == step.NodeId);
        var access = await snapshots.ResolveAsync(user, tenant, ct);
        var now = clock.GetUtcNow();
        if (!access!.Grants.Any(g => g.Code == "workflows.configure") && await Representation(step, node, request, user, now, ct) is null) throw ApplicationFault.NotFound();
        if (instance.Revision != input.Revision || instance.Status != "RUNNING" || step.Status != "PENDING" || step.PausedAt is not null) throw Conflict();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkflowAgentRun" SET "Status"='QUEUED',"NextAttemptAt"={now},"LeaseToken"=NULL,"LeaseUntil"=NULL,"ErrorCode"=NULL,"CompletedAt"=NULL
            WHERE "TenantId"={tenant} AND "StepId"={stepId} AND "Status" IN ('FAILED','REVIEW_REQUIRED') AND "Attempts"<9
            """, ct);
        if (affected != 1) throw Conflict();
        instance.Advance(input.Revision, now);
        db.AuditLogs.Add(AuditLog.WorkflowRuntimeChanged(tenant, user, requestId, "WORKFLOW.AGENT_RETRIED", new { stepId, input.Reason }, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await View(instance, user, ct);
    }

    /// <summary>Internal worker completion. No human decision is fabricated for an AI execution.</summary>
    internal async Task<bool> FinishAgentAsync(WorkflowAgentRow claimed, WorkflowAgentResult? result, string trace,
        string? error, string? model, DateTimeOffset started, CancellationToken ct)
    {
        var tenant = claimed.TenantId; var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await Lock(claimed.RequestId, tenant, ct);
        var rows = await db.Database.SqlQuery<WorkflowAgentRow>($"SELECT * FROM \"WorkflowAgentRun\" WHERE \"Id\"={claimed.Id} AND \"TenantId\"={tenant} FOR UPDATE").ToArrayAsync(ct);
        var job = rows.SingleOrDefault();
        if (job is null) return false;
        if (job.Status != "RUNNING" || job.LeaseToken != claimed.LeaseToken || job.LeaseUntil <= now)
        {
            // Preserve the returned work as stale evidence, without changing any step or newer run.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WorkflowAgentAttempt" ("RunId","Attempt","TenantId","StartedAt","CompletedAt","Model","ErrorCode","TraceJson")
                VALUES ({job.Id},{claimed.Attempts},{tenant},{started},{now},{model},'AI.STALE_LEASE',CAST({trace} AS jsonb))
                ON CONFLICT ("RunId","Attempt") DO NOTHING
                """, ct);
            await tx.CommitAsync(ct);
            return false;
        }
        var instance = await db.WorkflowInstances.SingleAsync(i => i.Id == job.InstanceId, ct);
        var step = await db.WorkflowExecutions.SingleAsync(s => s.Id == job.StepId, ct);
        var version = await db.WorkflowVersions.SingleAsync(v => v.Id == instance.WorkflowVersionId, ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        var node = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(n => n.GetProperty("id").GetString() == step.NodeId);
        var policy = WorkflowAgentContract.Policy(node);
        var active = await db.Tenants.AnyAsync(t => t.Id == tenant && t.Status == BizFlow.Domain.Tenancy.TenantStatus.Active &&
            db.Companies.Any(c => c.Id == t.CompanyId && c.Status == BizFlow.Domain.Tenancy.CompanyStatus.Active), ct);
        var status = "REVIEW_REQUIRED";
        var next = now;
        if (!active || instance.Status != "RUNNING" || step.Status != "PENDING") status = "SUPERSEDED";
        else if (step.PausedAt is not null) error = "AI.STEP_PAUSED";
        else if (policy is null || step.NodeType != "DEPARTMENT" || step.Policy != "SINGLE") error = "AI.POLICY_CHANGED";
        else if (error is not null)
        {
            status = error is "AI.NOT_CONFIGURED" or "AI.PROGRESSION_BLOCKED" ? "REVIEW_REQUIRED" : job.Attempts % 3 == 0 || job.Attempts >= 9 ? "FAILED" : "RETRY";
            next = now.AddMinutes(job.Attempts % 3 == 1 ? 1 : 5);
        }
        else if (result is not null && policy.Mode == "AUTO" && !result.NeedsHumanReview)
        {
            // A human may have already started an ALL decision or a candidate may have lost eligibility.
            var eligible = await Eligible(node, request, ct);
            if (!eligible.Intersect(step.Candidates()).Any() || await db.WorkflowDecisions.AnyAsync(d => d.StepExecutionId == step.Id, ct)) error = "AI.HUMAN_REQUIRED";
            else
            {
                step.Complete("COMPLETE", now);
                using var data = JsonDocument.Parse(instance.PayloadJson);
                var requester = await db.Users.SingleAsync(u => u.Id == request.RequesterId, ct);
                var resolution = engine.ResolveStepFromGraph(definition.RootElement, step.NodeId, "COMPLETE", data.RootElement, requester.Id, requester.DepartmentId);
                instance.Advance(instance.Revision, now);
                await Enter(instance, request, resolution, now, ct);
                request.ApplyRuntime(instance, now);
                status = "COMPLETED";
            }
        }
        var resultJson = result is null ? null : JsonSerializer.Serialize(result, WorkflowAgentContract.Json);
        DateTimeOffset? completed = status == "RETRY" ? null : now;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WorkflowAgentAttempt" ("RunId","Attempt","TenantId","StartedAt","CompletedAt","Model","ErrorCode","TraceJson")
            VALUES ({job.Id},{job.Attempts},{tenant},{started},{now},{model},{error},CAST({trace} AS jsonb));
            UPDATE "WorkflowAgentRun" SET "Status"={status},"ResultJson"=CAST({resultJson} AS jsonb),"ErrorCode"={error},"Model"={model},
              "CompletedAt"={completed},"NextAttemptAt"={next},"LeaseToken"=NULL,"LeaseUntil"=NULL WHERE "Id"={job.Id} AND "TenantId"={tenant};
            """, ct);
        db.AuditLogs.Add(AuditLog.WorkflowAgentChanged(tenant, request.Id, "WORKFLOW.AGENT_FINISHED",
            new { runId = job.Id, stepId = step.Id, job.Attempts, job.InputHash, status, error, model }, now));
        var candidateIds = step.Candidates().ToArray();
        var recipients = await db.Users.Where(u => candidateIds.Contains(u.Id) && u.Status == BizFlow.Domain.Organization.UserStatus.Active && u.DeletedAt == null)
            .Select(u => u.Id).ToArrayAsync(ct);
        if (status is "COMPLETED" or "REVIEW_REQUIRED" or "FAILED")
            foreach (var candidate in recipients)
                db.Notifications.Add(Notification.Create(tenant, candidate, NotificationEvent.Announcement,
                    status == "COMPLETED" ? "AI đã hoàn tất bước xử lý" : "Bước AI cần người phụ trách kiểm tra",
                    $"{request.Title}: {step.Name}. Mở tiến trình để xem kết quả và bằng chứng.", $"wf-agent:{job.Id:N}:{job.Attempts}:{candidate:N}", "Request", request.Id));
        await db.SaveChangesAsync(ct);
        await EnqueueAgents(instance, request, definition.RootElement, now, ct);
        await tx.CommitAsync(ct);
        try { await updates.PublishAsync(tenant, recipients, ct); await PublishPendingAsync(request.Id, ct); }
        catch (Exception e) when (e is not OperationCanceledException) { /* Durable inbox is already committed. */ }
        return true;
    }
}
