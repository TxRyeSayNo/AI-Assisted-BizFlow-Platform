using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Workflows;

public sealed class WorkflowLibraryStore(BizFlowDbContext db, ITenantContext context, TimeProvider clock) : IWorkflowLibraryStore
{
    private void AssertTenant(Guid tenantId)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenantId == Guid.Empty || tenantId != context.TenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Cấu hình quy trình yêu cầu không gian làm việc của công ty.");
    }

    public async Task<WorkflowPage> ListAsync(Guid tenantId, WorkflowFilter filter, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var query = db.Workflows.AsNoTracking().Where(w => w.TenantId == tenantId);
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(w => w.Name.ToUpper().Contains(normalized));
        }
        if (filter.BusinessType is { } type)
        {
            var value = Enum.Parse<WorkflowBusinessType>(type, true);
            query = query.Where(w => w.BusinessType == value);
        }
        if (filter.Status is { } status)
        {
            var value = Enum.Parse<WorkflowStatus>(status, true);
            query = query.Where(w => w.Status == value);
        }
        var total = await query.LongCountAsync(cancellationToken);
        var roots = await query.OrderBy(w => w.Name).ThenBy(w => w.Id).Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(w => new { w.Id, w.Name, w.BusinessType, w.Status,
                Latest = db.WorkflowVersions.Where(v => v.WorkflowId == w.Id).OrderByDescending(v => v.VersionNo)
                    .Select(v => new { v.Id, v.VersionNo, v.Status }).FirstOrDefault() }).ToListAsync(cancellationToken);
        return new(roots.Select(w => new WorkflowRow(w.Id, w.Name, w.BusinessType.ToString().ToUpperInvariant(), w.Status.ToString().ToUpperInvariant(),
            w.Latest?.Id, w.Latest?.VersionNo, w.Latest?.Status.ToString().ToUpperInvariant())).ToArray(), filter.Page, filter.PageSize, total);
    }

    public async Task<WorkflowVersionView?> ReadVersionAsync(Guid tenantId, Guid versionId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var metadata = await db.WorkflowVersions.AsNoTracking().Where(v => v.Id == versionId)
            .Join(db.Workflows.AsNoTracking().Where(w => w.TenantId == tenantId), v => v.WorkflowId, w => w.Id,
                (v, w) => new { Workflow = w, Version = v }).SingleOrDefaultAsync(cancellationToken);
        if (metadata is null) return null;
        var steps = await db.WorkflowSteps.AsNoTracking().Where(s => s.WorkflowVersionId == versionId).OrderBy(s => s.OrderNo).ThenBy(s => s.Id).ToListAsync(cancellationToken);
        var transitions = await db.WorkflowTransitions.AsNoTracking().Where(t => t.WorkflowVersionId == versionId)
            .OrderBy(t => t.FromState).ThenBy(t => t.ToState).ThenBy(t => t.Id).ToListAsync(cancellationToken);
        var version = metadata.Version;
        return new(metadata.Workflow.Id, metadata.Workflow.Name, metadata.Workflow.BusinessType.ToString().ToUpperInvariant(), version.Id, version.VersionNo,
            version.Status.ToString().ToUpperInvariant(), version.PublishedAt, Json(version.DefinitionJson),
            steps.Select(s => new WorkflowStepView(s.Id, s.StepCode, s.Name, s.Type.ToString().ToUpperInvariant(), s.OrderNo, Json(s.ConfigJson))).ToArray(),
            transitions.Select(t => new WorkflowTransitionView(t.Id, t.FromState, t.ToState, t.GuardJson is null ? null : Json(t.GuardJson))).ToArray());
    }

    public async Task<WorkflowRow> CreateAsync(WorkflowDefinition workflow, WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(workflow.TenantId);
        db.AddRange(workflow, version, audit);
        await db.SaveChangesAsync(cancellationToken);
        return new(workflow.Id, workflow.Name, workflow.BusinessType.ToString().ToUpperInvariant(), "DRAFT", version.Id, version.VersionNo, "DRAFT");
    }

    public async Task<IWorkflowVersionTransaction?> BeginNextVersionAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var workflow = await db.Workflows.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Workflow" WHERE "WorkflowId" = {workflowId}
                AND "TenantId" = {tenantId} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            if (workflow is null) { await transaction.DisposeAsync(); return null; }
            var latest = await db.WorkflowVersions.Where(v => v.WorkflowId == workflowId)
                .MaxAsync(v => (int?)v.VersionNo, cancellationToken) ?? 0;
            return new VersionTransaction(db, transaction, workflow, latest);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public async Task<WorkflowVersionView?> SaveDraftAsync(Guid tenantId, Guid versionId, string definitionJson,
        IReadOnlyList<WorkflowStepDraftInput>? steps, IReadOnlyList<WorkflowTransitionDraftInput>? transitions,
        string? name, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var version = await db.WorkflowVersions.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null) return null;
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == version.WorkflowId && w.TenantId == tenantId, cancellationToken);
        if (workflow is null) return null;
        if (version.Status != WorkflowVersionStatus.Draft)
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.VERSION_IMMUTABLE", "Chỉ bản nháp mới có thể chỉnh sửa cấu hình.");

        if (!string.IsNullOrWhiteSpace(name))
        {
            workflow.UpdateName(name.Trim(), clock.GetUtcNow());
        }

        version.UpdateDefinition(definitionJson);

        var existingSteps = await db.WorkflowSteps.Where(s => s.WorkflowVersionId == versionId).ToListAsync(cancellationToken);
        db.WorkflowSteps.RemoveRange(existingSteps);

        var existingTransitions = await db.WorkflowTransitions.Where(t => t.WorkflowVersionId == versionId).ToListAsync(cancellationToken);
        db.WorkflowTransitions.RemoveRange(existingTransitions);

        if (steps != null && steps.Count > 0)
        {
            foreach (var s in steps)
            {
                var type = Enum.Parse<WorkflowStepType>(s.Type, true);
                db.WorkflowSteps.Add(WorkflowStep.CreateDraft(versionId, s.StepCode, s.Name, type, s.OrderNo, s.ConfigJson));
            }
        }

        if (transitions != null && transitions.Count > 0)
        {
            foreach (var t in transitions)
            {
                db.WorkflowTransitions.Add(WorkflowTransition.CreateDraft(versionId, t.FromState, t.ToState, t.GuardJson));
            }
        }

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        return await ReadVersionAsync(tenantId, versionId, cancellationToken);
    }

    public async Task<WorkflowVersionView?> PublishVersionAsync(Guid tenantId, Guid versionId, Guid? associateServiceId, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var version = await db.WorkflowVersions.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null) return null;
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == version.WorkflowId && w.TenantId == tenantId, cancellationToken);
        if (workflow is null) return null;
        if (version.Status != WorkflowVersionStatus.Draft)
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.VERSION_ALREADY_PUBLISHED", "Phiên bản này đã được xuất bản.");

        var now = clock.GetUtcNow();
        version.Publish(now);
        workflow.Activate(now);

        if (associateServiceId.HasValue && workflow.BusinessType == WorkflowBusinessType.Request)
        {
            var service = await db.Services.FirstOrDefaultAsync(s => s.Id == associateServiceId.Value && s.TenantId == tenantId, cancellationToken);
            service?.SetActiveWorkflowVersion(version.Id, now);
        }

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        return await ReadVersionAsync(tenantId, versionId, cancellationToken);
    }

    public async Task<bool> DeleteWorkflowAsync(Guid tenantId, Guid workflowId, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, cancellationToken);
        if (workflow is null) return false;

        var versions = await db.WorkflowVersions.Where(v => v.WorkflowId == workflowId).ToListAsync(cancellationToken);
        if (versions.Any(v => v.Status == WorkflowVersionStatus.Published))
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.CANNOT_DELETE_PUBLISHED", "Không thể xóa quy trình đã có phiên bản xuất bản.");

        var versionIds = versions.Select(v => (Guid?)v.Id).ToList();
        if (await db.WorkTasks.AnyAsync(t => versionIds.Contains(t.WorkflowVersionId), cancellationToken) ||
            await db.Requests.AnyAsync(r => versionIds.Contains(r.WorkflowVersionId), cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.IN_USE", "Quy trình đang được liên kết với dữ liệu công việc trong hệ thống.");

        foreach (var v in versions)
        {
            var steps = await db.WorkflowSteps.Where(s => s.WorkflowVersionId == v.Id).ToListAsync(cancellationToken);
            db.WorkflowSteps.RemoveRange(steps);
            var transitions = await db.WorkflowTransitions.Where(t => t.WorkflowVersionId == v.Id).ToListAsync(cancellationToken);
            db.WorkflowTransitions.RemoveRange(transitions);
        }

        db.WorkflowVersions.RemoveRange(versions);
        db.Workflows.Remove(workflow);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WorkflowRow?> UpdateStatusAsync(Guid tenantId, Guid workflowId, WorkflowStatus status, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, cancellationToken);
        if (workflow is null) return null;

        var now = clock.GetUtcNow();
        workflow.UpdateStatus(status, now);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        var latest = await db.WorkflowVersions.Where(v => v.WorkflowId == workflow.Id).OrderByDescending(v => v.VersionNo)
            .Select(v => new { v.Id, v.VersionNo, v.Status }).FirstOrDefaultAsync(cancellationToken);

        return new(workflow.Id, workflow.Name, workflow.BusinessType.ToString().ToUpperInvariant(),
            workflow.Status.ToString().ToUpperInvariant(), latest?.Id, latest?.VersionNo, latest?.Status.ToString().ToUpperInvariant());
    }

    public async Task<WorkflowRow?> UpdateNameAsync(Guid tenantId, Guid workflowId, string name, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, cancellationToken);
        if (workflow is null) return null;

        var now = clock.GetUtcNow();
        workflow.UpdateName(name, now);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        var latest = await db.WorkflowVersions.Where(v => v.WorkflowId == workflow.Id).OrderByDescending(v => v.VersionNo)
            .Select(v => new { v.Id, v.VersionNo, v.Status }).FirstOrDefaultAsync(cancellationToken);

        return new(workflow.Id, workflow.Name, workflow.BusinessType.ToString().ToUpperInvariant(),
            workflow.Status.ToString().ToUpperInvariant(), latest?.Id, latest?.VersionNo, latest?.Status.ToString().ToUpperInvariant());
    }

    public async Task<IReadOnlyList<WorkflowLookupItem>> GetActiveDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        return await db.Departments.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
            .OrderBy(d => d.Name)
            .Select(d => new WorkflowLookupItem(d.Id, d.Name, d.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowLookupItem>> GetActiveRolesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        return await db.Roles.AsNoTracking()
            .Where(r => r.Status == RecordStatus.Active &&
                        (r.TenantId == tenantId || (r.IsSystem && r.Name != "PLATFORM_ADMIN")) &&
                        !db.RolePermissions.Any(rp => rp.RoleId == r.Id &&
                            db.Permissions.IgnoreQueryFilters().Any(p => p.Id == rp.PermissionId && p.ScopeType == PermissionScope.Platform)))
            .OrderBy(r => r.Name)
            .Select(r => new WorkflowLookupItem(r.Id, r.Name, null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowUserLookupItem>> GetActiveUsersAsync(Guid tenantId, Guid? departmentId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var query = db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsPlatformAdministrator && u.Status == UserStatus.Active && u.DeletedAt == null);

        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            query = query.Where(u => u.DepartmentId == departmentId.Value);
        }

        return await query
            .OrderBy(u => u.FullName)
            .Select(u => new WorkflowUserLookupItem(u.Id, u.FullName, u.EmployeeCode, u.Email, u.DepartmentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowServiceLookupItem>> GetServicesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        return await db.Services.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Name)
            .Select(s => new WorkflowServiceLookupItem(s.Id, s.Name, s.Code, s.ActiveWorkflowVersionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowVersionSummary>> GetWorkflowVersionsAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        return await db.WorkflowVersions.AsNoTracking()
            .Where(v => v.WorkflowId == workflowId)
            .OrderByDescending(v => v.VersionNo)
            .Select(v => new WorkflowVersionSummary(v.Id, v.VersionNo, v.Status.ToString().ToUpperInvariant(), v.PublishedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowDefinition?> GetWorkflowByIdAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        return await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, cancellationToken);
    }

    private sealed class VersionTransaction(BizFlowDbContext db, IDbContextTransaction transaction,
        WorkflowDefinition workflow, int latest) : IWorkflowVersionTransaction
    {
        public WorkflowDefinition Workflow => workflow;
        public int LatestVersionNo => latest;
        public async Task<WorkflowRow> CommitAsync(WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken)
        {
            if (version.WorkflowId != workflow.Id || (long)version.VersionNo != (long)latest + 1 ||
                version.Status != WorkflowVersionStatus.Draft || audit.TenantId != workflow.TenantId || audit.ObjectId != version.Id)
                throw new InvalidOperationException("The new draft and audit must belong to this workflow transaction.");
            db.AddRange(version, audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(workflow.Id, workflow.Name, workflow.BusinessType.ToString().ToUpperInvariant(),
                workflow.Status.ToString().ToUpperInvariant(), version.Id, version.VersionNo, "DRAFT");
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
