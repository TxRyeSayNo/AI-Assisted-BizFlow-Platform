using BizFlow.Application.Common;
using BizFlow.Application.Organization;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Organization;

public sealed class UserManagementStore(BizFlowDbContext db) : IUserManagementStore
{
    public Task<bool> EmployeeCodeExistsAsync(Guid tenantId, string employeeCode, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.TenantId == tenantId && u.NormalizedEmployeeCode == employeeCode && u.DeletedAt == null, cancellationToken);

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.TenantId == tenantId && u.NormalizedEmail == email && u.DeletedAt == null, cancellationToken);

    public Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(d => d.TenantId == tenantId && d.Id == departmentId && d.Status == RecordStatus.Active, cancellationToken);

    public async Task<UserOperationResult> CreateUserAsync(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken)
    {
        db.Users.Add(user);

        var assignedRoleIds = new List<Guid>();
        if (roleIds.Count == 0)
        {
            var defaultRole = await db.Roles.IgnoreQueryFilters()
                .SingleOrDefaultAsync(r => r.Name == "EMPLOYEE" && r.IsSystem, cancellationToken)
                ?? throw new InvalidOperationException("Default system role EMPLOYEE not found.");
            db.UserRoles.Add(new UserRole(user.Id, defaultRole.Id));
            assignedRoleIds.Add(defaultRole.Id);
        }
        else
        {
            var validRoles = await db.Roles
                .Where(r => roleIds.Contains(r.Id) && r.Status == RecordStatus.Active && (r.IsSystem || r.TenantId == user.TenantId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var roleId in validRoles)
            {
                db.UserRoles.Add(new UserRole(user.Id, roleId));
                assignedRoleIds.Add(roleId);
            }
        }

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        return new(user.Id, user.EmployeeCode, user.Email, user.FullName, user.DepartmentId, user.Status.ToString().ToUpperInvariant(), assignedRoleIds, user.CreatedAt);
    }

    public async Task<UserOperationResult> UpdateUserAsync(Guid tenantId, Guid userId, string fullName, Guid? departmentId, IReadOnlyList<Guid>? roleIds, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (user is null) throw ApplicationFault.NotFound();

        user.UpdateProfile(fullName, departmentId, now);

        var assignedRoleIds = new List<Guid>();
        if (roleIds is not null)
        {
            var currentLinks = await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);
            db.UserRoles.RemoveRange(currentLinks);

            var validRoles = await db.Roles
                .Where(r => roleIds.Contains(r.Id) && r.Status == RecordStatus.Active && (r.IsSystem || r.TenantId == tenantId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var roleId in validRoles)
            {
                db.UserRoles.Add(new UserRole(user.Id, roleId));
                assignedRoleIds.Add(roleId);
            }
        }
        else
        {
            assignedRoleIds = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);
        }

        db.AuditLogs.Add(AuditLog.UserUpdated(tenantId, actorId, userId, now));
        await db.SaveChangesAsync(cancellationToken);

        return new(user.Id, user.EmployeeCode, user.Email, user.FullName, user.DepartmentId, user.Status.ToString().ToUpperInvariant(), assignedRoleIds, user.CreatedAt);
    }

    public async Task<UserOperationResult> UpdateStatusAsync(Guid tenantId, Guid userId, UserStatus newStatus, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (user is null) throw ApplicationFault.NotFound();

        user.SetStatus(newStatus, now);

        var assignedRoleIds = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);

        db.AuditLogs.Add(AuditLog.UserStatusChanged(tenantId, actorId, userId, newStatus.ToString().ToUpperInvariant(), now));
        await db.SaveChangesAsync(cancellationToken);

        return new(user.Id, user.EmployeeCode, user.Email, user.FullName, user.DepartmentId, user.Status.ToString().ToUpperInvariant(), assignedRoleIds, user.CreatedAt);
    }
}
