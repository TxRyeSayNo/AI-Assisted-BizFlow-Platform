using BizFlow.Application.Common;
using BizFlow.Application.Organization;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Organization;

public sealed class DepartmentManagementStore(BizFlowDbContext db) : IDepartmentManagementStore
{
    public Task<bool> DepartmentCodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(d => d.TenantId == tenantId && d.Code == code, cancellationToken);

    public Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(d => d.TenantId == tenantId && d.Id == departmentId && d.Status == RecordStatus.Active, cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid departmentId, Guid proposedParentId, CancellationToken cancellationToken)
    {
        var currentId = (Guid?)proposedParentId;
        var visited = new HashSet<Guid>();

        while (currentId is { } parent)
        {
            if (parent == departmentId) return true;
            if (!visited.Add(parent)) break;

            currentId = await db.Departments
                .Where(d => d.TenantId == tenantId && d.Id == parent)
                .Select(d => d.ParentDepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public async Task<DepartmentOperationResult> CreateAsync(Department department, AuditLog audit, CancellationToken cancellationToken)
    {
        db.Departments.Add(department);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        return new(
            department.Id,
            department.Code,
            department.Name,
            department.ParentDepartmentId,
            department.Status.ToString().ToUpperInvariant(),
            department.CreatedAt,
            department.UpdatedAt);
    }

    public async Task<DepartmentOperationResult> UpdateAsync(Guid tenantId, Guid departmentId, string name, Guid? parentId, RecordStatus status, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId && d.TenantId == tenantId, cancellationToken);
        if (department is null) throw ApplicationFault.NotFound();

        department.Update(name, parentId, status, now);
        db.AuditLogs.Add(AuditLog.DepartmentUpdated(tenantId, actorId, departmentId, department.Code, department.Name, now));
        await db.SaveChangesAsync(cancellationToken);

        return new(
            department.Id,
            department.Code,
            department.Name,
            department.ParentDepartmentId,
            department.Status.ToString().ToUpperInvariant(),
            department.CreatedAt,
            department.UpdatedAt);
    }
}
