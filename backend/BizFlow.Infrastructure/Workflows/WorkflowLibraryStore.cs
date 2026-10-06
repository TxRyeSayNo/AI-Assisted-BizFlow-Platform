using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Workflows;

public sealed class WorkflowLibraryStore(BizFlowDbContext db, ITenantContext context) : IWorkflowLibraryStore
{
    private void AssertTenant(Guid tenantId)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenantId == Guid.Empty || tenantId != context.TenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Workflow configuration requires a tenant workspace.");
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
        // A consistent snapshot avoids mixing steps/transitions from separate draft revisions.
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
        // One EF transaction for the root, first draft version and immutable actor-attributed audit.
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
            // All next-version writers serialize on the same tenant-owned parent row.
            // Read max only after acquiring the lock so each waiter sees the previous commit.
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
