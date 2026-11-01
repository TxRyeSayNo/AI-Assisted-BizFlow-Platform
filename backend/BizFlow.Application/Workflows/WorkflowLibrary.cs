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

public sealed record WorkflowStepDraftInput(string StepCode, string Name, string Type, int OrderNo, string ConfigJson = "{}");
public sealed record WorkflowTransitionDraftInput(string FromState, string ToState, string? GuardJson = null);
public sealed record WorkflowSaveDraftInput(string DefinitionJson, IReadOnlyList<WorkflowStepDraftInput>? Steps = null, IReadOnlyList<WorkflowTransitionDraftInput>? Transitions = null, string? Name = null);
public sealed record WorkflowPublishInput(Guid? ServiceId = null);
public sealed record WorkflowLookupItem(Guid Id, string Name, string? Code = null);
public sealed record WorkflowServiceLookupItem(Guid Id, string Name, string Code, Guid? ActiveWorkflowVersionId);
public sealed record WorkflowVersionSummary(Guid VersionId, int VersionNo, string Status, DateTimeOffset? PublishedAt);
public sealed record WorkflowValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public interface IWorkflowLibraryStore
{
    Task<WorkflowPage> ListAsync(Guid tenantId, WorkflowFilter filter, CancellationToken cancellationToken);
    Task<WorkflowVersionView?> ReadVersionAsync(Guid tenantId, Guid versionId, CancellationToken cancellationToken);
    Task<WorkflowRow> CreateAsync(WorkflowDefinition workflow, WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken);
    Task<IWorkflowVersionTransaction?> BeginNextVersionAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken);
    Task<WorkflowVersionView?> SaveDraftAsync(Guid tenantId, Guid versionId, string definitionJson, IReadOnlyList<WorkflowStepDraftInput>? steps, IReadOnlyList<WorkflowTransitionDraftInput>? transitions, string? name, AuditLog audit, CancellationToken cancellationToken);
    Task<WorkflowRow?> UpdateNameAsync(Guid tenantId, Guid workflowId, string name, AuditLog audit, CancellationToken cancellationToken);
    Task<WorkflowVersionView?> PublishVersionAsync(Guid tenantId, Guid versionId, Guid? associateServiceId, AuditLog audit, CancellationToken cancellationToken);
    Task<bool> DeleteWorkflowAsync(Guid tenantId, Guid workflowId, AuditLog audit, CancellationToken cancellationToken);
    Task<WorkflowRow?> UpdateStatusAsync(Guid tenantId, Guid workflowId, WorkflowStatus status, AuditLog audit, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowLookupItem>> GetActiveDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowLookupItem>> GetActiveRolesAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowServiceLookupItem>> GetServicesAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowVersionSummary>> GetWorkflowVersionsAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken);
    Task<WorkflowDefinition?> GetWorkflowByIdAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken);
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
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Cấu hình quy trình yêu cầu không gian làm việc của công ty.");
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
        await AuthorizeAsync(ConfigurePermission, cancellationToken);
        if (transaction.LatestVersionNo == int.MaxValue)
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.VERSION_LIMIT", "Quy trình này đã đạt giới hạn số lượng phiên bản được hỗ trợ.");
        var version = WorkflowVersion.CreateDraft(transaction.Workflow.Id, transaction.LatestVersionNo + 1);
        var entry = AuditLog.WorkflowVersionDraftCreated(tenantId, context.UserId!.Value, workflowId,
            version.Id, version.VersionNo, clock.GetUtcNow());
        return await transaction.CommitAsync(version, entry, cancellationToken);
    }

    public async Task<WorkflowVersionView> SaveDraftAsync(Guid versionId, WorkflowSaveDraftInput input, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        var existing = await store.ReadVersionAsync(tenantId, versionId, cancellationToken);
        if (existing is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        if (existing.Status != "DRAFT")
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.VERSION_IMMUTABLE", "Chỉ bản nháp mới có thể chỉnh sửa cấu hình.");

        var now = clock.GetUtcNow();
        var auditEntry = AuditLog.WorkflowDraftSaved(tenantId, context.UserId!.Value, existing.WorkflowId, existing.VersionId, existing.VersionNo, now);
        var result = await store.SaveDraftAsync(tenantId, versionId, input.DefinitionJson, input.Steps, input.Transitions, input.Name, auditEntry, cancellationToken);
        return result ?? throw ApplicationFault.NotFound();
    }

    public async Task<WorkflowValidationResult> ValidateVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        var version = await store.ReadVersionAsync(tenantId, versionId, cancellationToken);
        if (version is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        var departments = await store.GetActiveDepartmentsAsync(tenantId, cancellationToken);
        var roles = await store.GetActiveRolesAsync(tenantId, cancellationToken);

        var deptIds = departments.Select(d => d.Id).ToHashSet();
        var roleIds = roles.Select(r => r.Id).ToHashSet();

        var businessType = Enum.Parse<WorkflowBusinessType>(version.BusinessType, true);
        var definitionJson = version.Definition.GetRawText();

        return WorkflowValidator.Validate(definitionJson, businessType, deptIds, roleIds);
    }

    public async Task<WorkflowVersionView> PublishVersionAsync(Guid versionId, WorkflowPublishInput input, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        var version = await store.ReadVersionAsync(tenantId, versionId, cancellationToken);
        if (version is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        if (version.Status != "DRAFT")
            throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.VERSION_ALREADY_PUBLISHED", "Phiên bản này đã được xuất bản trước đó.");

        // Chạy kiểm tra hợp lệ toàn bộ sơ đồ
        var validation = await ValidateVersionAsync(versionId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.VALIDATION_FAILED",
                string.Join("; ", validation.Errors));
        }

        // Nếu có gắn dịch vụ, kiểm tra dịch vụ thuộc cùng Tenant
        if (input.ServiceId.HasValue)
        {
            var services = await store.GetServicesAsync(tenantId, cancellationToken);
            if (!services.Any(s => s.Id == input.ServiceId.Value))
            {
                throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.INVALID_SERVICE", "Dịch vụ được chọn không thuộc công ty hiện tại.");
            }
        }

        var now = clock.GetUtcNow();
        var auditEntry = AuditLog.WorkflowVersionPublished(tenantId, context.UserId!.Value, version.WorkflowId, version.VersionId, version.VersionNo, input.ServiceId, now);
        var result = await store.PublishVersionAsync(tenantId, versionId, input.ServiceId, auditEntry, cancellationToken);
        return result ?? throw ApplicationFault.NotFound();
    }

    public async Task<bool> DeleteWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        var workflow = await store.GetWorkflowByIdAsync(tenantId, workflowId, cancellationToken);
        if (workflow is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        var now = clock.GetUtcNow();
        var auditEntry = AuditLog.WorkflowDraftDeleted(tenantId, context.UserId!.Value, workflowId, workflow.Name, now);
        return await store.DeleteWorkflowAsync(tenantId, workflowId, auditEntry, cancellationToken);
    }

    public async Task<WorkflowRow> UpdateStatusAsync(Guid workflowId, string status, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        if (status is not ("ACTIVE" or "INACTIVE"))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Trạng thái hợp lệ gồm ACTIVE hoặc INACTIVE.");

        var parsedStatus = Enum.Parse<WorkflowStatus>(status, true);
        var now = clock.GetUtcNow();
        var auditEntry = AuditLog.WorkflowStatusUpdated(tenantId, context.UserId!.Value, workflowId, status, now);
        var result = await store.UpdateStatusAsync(tenantId, workflowId, parsedStatus, auditEntry, cancellationToken);
        if (result is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        return result;
    }

    public async Task<WorkflowRow> UpdateNameAsync(Guid workflowId, string name, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Tên quy trình tối đa 200 ký tự và không được để trống.");

        var now = clock.GetUtcNow();
        var auditEntry = AuditLog.WorkflowNameUpdated(tenantId, context.UserId!.Value, workflowId, name.Trim(), now);
        var result = await store.UpdateNameAsync(tenantId, workflowId, name.Trim(), auditEntry, cancellationToken);
        if (result is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        return result;
    }

    public async Task<IReadOnlyList<WorkflowLookupItem>> GetActiveDepartmentsAsync(CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        return await store.GetActiveDepartmentsAsync(tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowLookupItem>> GetActiveRolesAsync(CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        return await store.GetActiveRolesAsync(tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowServiceLookupItem>> GetServicesAsync(CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        return await store.GetServicesAsync(tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowVersionSummary>> GetWorkflowVersionsAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ReadPermission, cancellationToken);
        return await store.GetWorkflowVersionsAsync(tenantId, workflowId, cancellationToken);
    }

    private static ApplicationFault Validation() => new(FaultKind.Validation, "VALIDATION.FAILED", "Tên quy trình tối đa 200 ký tự, loại nghiệp vụ và trạng thái hợp lệ, kích thước trang từ 1 đến 100.");
}
