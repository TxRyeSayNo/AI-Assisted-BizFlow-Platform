using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Organization;

public sealed record CreateDepartmentRequest(string Code, string Name, Guid? ParentDepartmentId = null);
public sealed record UpdateDepartmentRequest(string Name, Guid? ParentDepartmentId = null, string Status = "ACTIVE");

public sealed record DepartmentOperationResult(
    Guid DepartmentId,
    string Code,
    string Name,
    Guid? ParentDepartmentId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IDepartmentManagementStore
{
    Task<bool> DepartmentCodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken);
    Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken);
    Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid departmentId, Guid proposedParentId, CancellationToken cancellationToken);
    Task<DepartmentOperationResult> CreateAsync(Department department, AuditLog audit, CancellationToken cancellationToken);
    Task<DepartmentOperationResult> UpdateAsync(Guid tenantId, Guid departmentId, string name, Guid? parentId, RecordStatus status, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class DepartmentManagement(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IDepartmentManagementStore store,
    TimeProvider clock)
{
    public const string ManagePermission = "roles.configure";

    private async Task<Guid> AuthorizeAsync(CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(ManagePermission, new ResourceScope(context.TenantId), cancellationToken: cancellationToken);
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Department management requires a tenant workspace.");
    }

    public async Task<DepartmentOperationResult> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 50)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Department code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Department name is required and must not exceed 200 characters.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await store.DepartmentCodeExistsAsync(tenantId, normalizedCode, cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "DEPARTMENT.CODE_IN_USE", "A department with this code already exists in your company.");

        if (request.ParentDepartmentId is { } parentId && !await store.DepartmentExistsAsync(tenantId, parentId, cancellationToken))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "The specified parent department does not exist.");

        var now = clock.GetUtcNow();
        var department = Department.Create(tenantId, normalizedCode, request.Name.Trim(), request.ParentDepartmentId, now);
        var audit = AuditLog.DepartmentCreated(tenantId, context.UserId!.Value, department.Id, department.Code, department.Name, now);

        return await store.CreateAsync(department, audit, cancellationToken);
    }

    public async Task<DepartmentOperationResult> UpdateAsync(Guid departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Department name is required and must not exceed 200 characters.");

        if (!Enum.TryParse<RecordStatus>(request.Status, true, out var status))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Valid status values are: ACTIVE, INACTIVE.");

        if (request.ParentDepartmentId is { } parentId)
        {
            if (parentId == departmentId)
                throw new ApplicationFault(FaultKind.Validation, "DEPARTMENT.CYCLE_DETECTED", "A department cannot be its own parent.");

            if (!await store.DepartmentExistsAsync(tenantId, parentId, cancellationToken))
                throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "The specified parent department does not exist.");

            if (await store.WouldCreateCycleAsync(tenantId, departmentId, parentId, cancellationToken))
                throw new ApplicationFault(FaultKind.Conflict, "DEPARTMENT.CYCLE_DETECTED", "Setting this parent department would create a cyclical hierarchy.");
        }

        var now = clock.GetUtcNow();
        return await store.UpdateAsync(tenantId, departmentId, request.Name.Trim(), request.ParentDepartmentId, status, context.UserId!.Value, now, cancellationToken);
    }
}
