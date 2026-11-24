using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Workflows;

public sealed partial class WorkflowRuntimeStore
{
    public async Task<WorkflowRuntimeView> PauseSlaAsync(Guid requestId, Guid stepId, WorkflowSlaInput input, bool resume, CancellationToken ct)
    {
        var (tenant,user) = await Identity(ct);
        if (!resume && (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length>1000 || input.Reason.Any(char.IsControl)))
            throw Invalid("Cần lý do tạm dừng SLA, tối đa 1000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await Lock(requestId,tenant,ct);
        var instance = await db.WorkflowInstances.SingleOrDefaultAsync(i=>i.RequestId==requestId,ct) ?? throw ApplicationFault.NotFound();
        var step = await db.WorkflowExecutions.SingleOrDefaultAsync(s=>s.Id==stepId && s.InstanceId==instance.Id,ct) ?? throw ApplicationFault.NotFound();
        var version = await db.WorkflowVersions.SingleAsync(v=>v.Id==instance.WorkflowVersionId,ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        var node = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(n=>n.GetProperty("id").GetString()==step.NodeId);
        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        var access = await snapshots.ResolveAsync(user,tenant,ct);
        if (!access!.Grants.Any(g=>g.Code=="workflows.configure"))
        {
            var representation = await Representation(step,node,request,user,now,ct);
            if (representation is null || await db.WorkflowDecisions.AnyAsync(d=>d.StepExecutionId==stepId &&
                (d.ActorId==user || d.PrincipalId==representation.Value.Principal),ct)) throw ApplicationFault.NotFound();
        }
        if (instance.Status!="RUNNING" || instance.Revision!=input.Revision || step.Status!="PENDING" || step.DueAt is null ||
            resume != (step.PausedAt is not null)) throw Conflict();
        WorkflowSlaPause pause;
        if (resume)
        {
            pause = await db.WorkflowSlaPauses.SingleAsync(p=>p.StepExecutionId==stepId && p.ResumedAt==null,ct);
            try { pause.Resume(step,user,now); }
            catch (ArgumentException) { throw Invalid("Khoảng tạm dừng quá dài hoặc lịch tính SLA không hợp lệ."); }
        }
        else
        {
            var calendarId = Id(definition.RootElement,"calendarId");
            var calendar = calendarId is null ? null : await db.BusinessCalendars.AsNoTracking().SingleAsync(c=>c.Id==calendarId,ct);
            pause = WorkflowSlaPause.Begin(step,user,input.Reason!,now,calendar); db.WorkflowSlaPauses.Add(pause);
        }
        instance.Advance(input.Revision,now);
        db.AuditLogs.Add(AuditLog.WorkflowRuntimeChanged(tenant,user,requestId,resume?"WORKFLOW.SLA_RESUMED":"WORKFLOW.SLA_PAUSED",
            new {pause.Id,stepId,pause.Reason,pause.PausedAt,pause.ResumedAt,pause.DueAtBefore,pause.DueAtAfter,instance.Revision},now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await PublishPendingAsync(requestId,ct);
        return await View(instance,user,ct);
    }
}
