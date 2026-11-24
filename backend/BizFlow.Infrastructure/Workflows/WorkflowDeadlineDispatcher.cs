using System.Text.Json;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Workflows;

public interface IWorkflowDeadlineDispatcher
{
    Task<int> SweepAsync(CancellationToken ct);
    Task<int> SweepTenantAsync(Guid tenantId,CancellationToken ct);
}

/// <summary>In-app reminders and manager alerts. Request-row locks serialize against decisions, holds and concurrent sweeps.</summary>
public sealed class WorkflowDeadlineDispatcher(DbContextOptions<BizFlowDbContext> options,INotificationUpdates updates,
    TimeProvider clock,IConfiguration configuration,ILogger<WorkflowDeadlineDispatcher> logger) : IWorkflowDeadlineDispatcher
{
    private sealed record JobContext(Guid? TenantId) : ITenantContext
    { public Guid? UserId=>new Guid("00000000-0000-0000-0000-000000000001"); }
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        if (!configuration.GetValue("Features:DeadlineReminders",true)) return 0;
        await using var db=new BizFlowDbContext(options,new JobContext(null));
        var tenants=await db.Database.SqlQuery<Guid>($"SELECT \"TenantId\" AS \"Value\" FROM \"Tenant\" WHERE \"Status\"='ACTIVE'").ToArrayAsync(ct);
        var total=0;
        foreach(var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();
            try { total+=await SweepTenantAsync(tenant,ct); }
            catch(Exception error) when(error is not OperationCanceledException) { logger.LogWarning(error,"Workflow deadline sweep failed for tenant {TenantId}.",tenant); }
        }
        return total;
    }
    public async Task<int> SweepTenantAsync(Guid tenantId,CancellationToken ct)
    {
        if (!configuration.GetValue("Features:DeadlineReminders",true)) return 0;
        await using var scan=new BizFlowDbContext(options,new JobContext(tenantId));
        if (!await scan.Tenants.AnyAsync(t=>t.Status==BizFlow.Domain.Tenancy.TenantStatus.Active,ct)) return 0;
        var cutoff=clock.GetUtcNow().AddHours(4);Guid? after=null;var total=0;
        while(true)
        {
            // Keyset paging scans every due step; old, already-notified rows cannot starve newer work.
            var batch=await scan.WorkflowExecutions.AsNoTracking().Where(s=>s.Status=="PENDING" && s.PausedAt==null && s.DueAt<=cutoff &&
                (after==null || s.Id.CompareTo(after.Value)>0) && scan.WorkflowInstances.Any(i=>i.Id==s.InstanceId && i.Status=="RUNNING"))
                .OrderBy(s=>s.Id).Select(s=>new{s.Id,s.InstanceId}).Take(200).ToArrayAsync(ct);
            if(batch.Length==0)break;
            foreach(var item in batch)
            {
                ct.ThrowIfCancellationRequested();
                total+=await Notify(tenantId,item.Id,item.InstanceId,ct);after=item.Id;
            }
        }
        return total;
    }
    private async Task<int> Notify(Guid tenant,Guid stepId,Guid instanceId,CancellationToken ct)
    {
        var context=new JobContext(tenant);await using var db=new BizFlowDbContext(options,context);
        var requestId=await db.WorkflowInstances.Where(i=>i.Id==instanceId).Select(i=>i.RequestId).SingleOrDefaultAsync(ct);
        if(requestId==Guid.Empty)return 0;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var request=await db.Requests.FromSqlInterpolated($"SELECT *, xmin FROM \"Request\" WHERE \"RequestId\"={requestId} AND \"TenantId\"={tenant} AND \"DeletedAt\" IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
        if(request is null)return 0;
        var instance=await db.WorkflowInstances.SingleAsync(i=>i.Id==instanceId,ct);
        var step=await db.WorkflowExecutions.SingleAsync(s=>s.Id==stepId,ct);var now=clock.GetUtcNow();
        if(instance.Status!="RUNNING" || step.Status!="PENDING" || step.PausedAt is not null || step.DueAt is not {} due || due>now.AddHours(4))return 0;
        var version=await db.WorkflowVersions.SingleAsync(v=>v.Id==instance.WorkflowVersionId,ct);
        using var definition=JsonDocument.Parse(version.DefinitionJson);
        var escalationHours=WorkflowDeadlinePolicy.EscalationHours(definition.RootElement);
        var kind=due<=now?"overdue":due-now<=TimeSpan.FromHours(1)?"1h":"4h";
        var decisions=await db.WorkflowDecisions.Where(d=>d.StepExecutionId==stepId).Select(d=>d.PrincipalId).ToArrayAsync(ct);
        var delegations=await db.WorkflowDelegations.Where(d=>d.StepExecutionId==stepId && d.RevokedAt==null && d.CreatedAt<=now && d.ExpiresAt>now).ToArrayAsync(ct);
        var people=step.Candidates().Except(decisions).Select(id=>delegations.SingleOrDefault(d=>d.PrincipalId==id)?.DelegateId ?? id).Distinct().ToArray();
        var recipients=await db.Users.Where(u=>people.Contains(u.Id) && u.Status==UserStatus.Active && u.DeletedAt==null).Select(u=>u.Id).ToListAsync(ct);
        var keys=(await db.Database.SqlQuery<string>($"SELECT \"IdempotencyKey\" AS \"Value\" FROM \"Notification\" WHERE \"TenantId\"={tenant} AND \"ObjectType\"='Request' AND \"ObjectId\"={requestId}").ToArrayAsync(ct)).ToHashSet();
        var notified=new HashSet<Guid>();
        void Add(Guid recipient,string mark,NotificationEvent type,string title)
        {
            var key=$"wf:{stepId:N}:{due.UtcTicks}:{mark}:{recipient:N}";
            if(!keys.Add(key))return;
            db.Notifications.Add(Notification.Create(tenant,recipient,type,title,$"{request.Title}: {step.Name}. Hạn xử lý (UTC): {due:dd/MM/yyyy HH:mm}.",key,"Request",requestId));
            notified.Add(recipient);
        }
        foreach(var recipient in recipients)Add(recipient,kind,kind=="overdue"?NotificationEvent.Overdue:NotificationEvent.DeadlineWarning,kind=="overdue"?"Bước quy trình đã quá hạn":"Bước quy trình sắp đến hạn");
        if(now-due>=TimeSpan.FromHours(escalationHours))
        {
            var department=await db.Users.Where(u=>u.Id==request.RequesterId).Select(u=>u.DepartmentId).SingleAsync(ct);
            var hierarchy=await db.Departments.Where(d=>d.Status==RecordStatus.Active).Select(d=>new DepartmentHierarchyNode(d.Id,d.ParentDepartmentId)).ToListAsync(ct);
            var scopes=await db.ManagementScopes.ToListAsync(ct);
            var scopedManagers=department is null?Array.Empty<Guid>():scopes.GroupBy(s=>s.UserId).Where(g=>ManagementScopeExpansion.Expand(hierarchy,
                g.Select(s=>new ManagementScopeRoot(s.DepartmentId,s.IncludeDescendants)).ToArray()).Contains(department.Value)).Select(g=>g.Key).ToArray();
            // Only notify managers who can actually read this request; administrative tenant readers are the fallback.
            var managers=await db.Users.Where(u=>u.Status==UserStatus.Active && u.DeletedAt==null && db.UserRoles.Any(ur=>ur.UserId==u.Id &&
                db.Roles.Any(r=>r.Id==ur.RoleId && r.Status==RecordStatus.Active) && db.RolePermissions.Any(rp=>rp.RoleId==ur.RoleId &&
                    db.Permissions.Any(p=>p.Id==rp.PermissionId && (p.Code=="requests.read.tenant" || p.Code=="requests.read.managed" && scopedManagers.Contains(u.Id))))))
                .Select(u=>u.Id).ToArrayAsync(ct);
            foreach(var manager in managers)Add(manager,"escalation",NotificationEvent.Escalation,"Cần xem xét bước quy trình quá hạn");
        }
        var count=db.ChangeTracker.Entries<Notification>().Count(e=>e.State==EntityState.Added);
        if(count>0)db.AuditLogs.Add(AuditLog.WorkflowDeadlineRaised(tenant,context.UserId!.Value,requestId,new{stepId,dueAt=due,kind,recipients=notified.ToArray()},now));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        if(notified.Count>0)
            try { await updates.PublishAsync(tenant,notified.ToArray(),ct); }
            catch(Exception error) when(error is not OperationCanceledException) { logger.LogWarning(error,"Workflow reminder was stored but realtime publish failed for request {RequestId}.",requestId); }
        return count;
    }
}
