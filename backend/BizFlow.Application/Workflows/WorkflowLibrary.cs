using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Workflows;

namespace BizFlow.Application.Workflows;

public sealed record WorkflowFilter(int Page = 1, int PageSize = 25, string? Search = null, string? BusinessType = null, string? Status = null);
public sealed record WorkflowRow(Guid WorkflowId, string Name, string BusinessType, string Status, Guid? LatestVersionId, int? LatestVersionNo, string? LatestVersionStatus);
public sealed record WorkflowPage(IReadOnlyList<WorkflowRow> Items, int Page, int PageSize, long Total);
public sealed record WorkflowStepView(Guid StepId, string StepCode, string Name, string Type, int OrderNo, JsonElement Config);
public sealed record WorkflowTransitionView(Guid TransitionId, string FromState, string ToState, JsonElement? Guard);
public sealed record WorkflowVersionView(Guid WorkflowId, string Name, string BusinessType, Guid VersionId, int VersionNo,
    string Status, DateTimeOffset? PublishedAt, JsonElement Definition, IReadOnlyList<WorkflowStepView> Steps, IReadOnlyList<WorkflowTransitionView> Transitions);

public interface IWorkflowLibraryStore
{
    Task<WorkflowPage> ListAsync(Guid tenantId, WorkflowFilter filter, CancellationToken cancellationToken);
    Task<WorkflowVersionView?> ReadVersionAsync(Guid tenantId, Guid versionId, CancellationToken cancellationToken);
    Task<WorkflowRow> CreateAsync(WorkflowDefinition workflow, WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken);
    Task<IWorkflowVersionTransaction?> BeginNextVersionAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken);
}

public interface IWorkflowVersionTransaction : IAsyncDisposable
{
    WorkflowDefinition Workflow { get; }
    int LatestVersionNo { get; }
    Task<WorkflowRow> CommitAsync(WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken);
}

public sealed class WorkflowLibrary(ITenantContext context, IResourceAuthorizer authorizer, ISecurityAuditWriter audit,
    IWorkflowLibraryStore store, TimeProvider clock)
{
    public const string ReadPermission = "workflows.read";
    public const string ConfigurePermission = "workflows.configure";

    private async Task<Guid> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(permission, new(context.TenantId), cancellationToken: cancellationToken);
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Workflow configuration requires a tenant workspace.");
    }

    public async Task<WorkflowPage> ListAsync(WorkflowFilter filter, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ReadPermission, cancellationToken);
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var type = string.IsNullOrWhiteSpace(filter.BusinessType) ? null : filter.BusinessType.Trim();
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            search?.Length > 200 || type is not (null or "TASK" or "REQUEST") || status is not (null or "DRAFT" or "ACTIVE" or "INACTIVE"))
            throw Validation();
        return await store.ListAsync(tenantId, filter with { Search = search, BusinessType = type, Status = status }, cancellationToken);
    }

    public async Task<WorkflowVersionView> ReadVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ReadPermission, cancellationToken);
        var version = await store.ReadVersionAsync(tenantId, versionId, cancellationToken);
        if (version is not null) return version;
        // Missing and other-tenant IDs have the same response and caller-only denial evidence.
        await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ReadPermission, AccessDenial.ResourceNotFound, cancellationToken);
        throw ApplicationFault.NotFound();
    }

    public async Task<WorkflowRow> CreateAsync(string name, string businessType, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 || businessType is not ("TASK" or "REQUEST")) throw Validation();
        var now = clock.GetUtcNow();
        var workflow = WorkflowDefinition.CreateDraft(tenantId, name, businessType == "TASK" ? WorkflowBusinessType.Task : WorkflowBusinessType.Request, now);
        var version = WorkflowVersion.CreateDraft(workflow.Id, 1);
        return await store.CreateAsync(workflow, version, AuditLog.WorkflowDraftCreated(tenantId, context.UserId!.Value,
            workflow.Id, workflow.Name, businessType, version.Id, now), cancellationToken);
    }

    public async Task<WorkflowRow> CreateNextVersionAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        await using var transaction = await store.BeginNextVersionAsync(tenantId, workflowId, cancellationToken);
        if (transaction is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        // Recheck live authority after waiting for the aggregate lock.
        await AuthorizeAsync(ConfigurePermission, cancellationToken);
        if (transaction.LatestVersionNo == int.MaxValue)
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.VERSION_LIMIT", "This workflow has reached the supported version-number limit.");
        var version = WorkflowVersion.CreateDraft(transaction.Workflow.Id, transaction.LatestVersionNo + 1);
        var entry = AuditLog.WorkflowVersionDraftCreated(tenantId, context.UserId!.Value, workflowId,
            version.Id, version.VersionNo, clock.GetUtcNow());
        return await transaction.CommitAsync(version, entry, cancellationToken);
    }

    private static ApplicationFault Validation() => new(FaultKind.Validation, "VALIDATION.FAILED", "Use a workflow name up to 200 characters, a supported type/status and a page size from 1 to 100.");
}
