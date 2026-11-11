using BizFlow.Application.Common;
using BizFlow.Application.Organization;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
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

    public async Task<IReadOnlyList<DepartmentLookup>> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.Departments
            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
            .Select(d => new DepartmentLookup(d.Id, d.Code, d.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoleLookup>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.Roles
            .Where(r => (r.TenantId == tenantId || r.IsSystem) && r.Status == RecordStatus.Active)
            .Select(r => new RoleLookup(r.Id, r.Name))
            .ToListAsync(cancellationToken);

    public async Task<int> BatchCreateUsersAsync(IReadOnlyList<(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit)> users, CancellationToken cancellationToken)
    {
        var defaultEmployeeRole = await db.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == "EMPLOYEE" && r.IsSystem, cancellationToken);

        foreach (var (user, roleIds, audit) in users)
        {
            db.Users.Add(user);
            if (roleIds.Count == 0 && defaultEmployeeRole != null)
            {
                db.UserRoles.Add(new UserRole(user.Id, defaultEmployeeRole.Id));
            }
            else
            {
                foreach (var roleId in roleIds)
                {
                    db.UserRoles.Add(new UserRole(user.Id, roleId));
                }
            }
            db.AuditLogs.Add(audit);
        }

        await db.SaveChangesAsync(cancellationToken);
        return users.Count;
    }

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

    public async Task<UserResponsibilitiesView> GetResponsibilitiesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (user is null) throw ApplicationFault.NotFound();

        string? deptName = null;
        if (user.DepartmentId is { } dId)
        {
            deptName = await db.Departments.Where(d => d.TenantId == tenantId && d.Id == dId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken);
        }

        var activeMainTasks = await (from t in db.WorkTasks
                                     join a in db.TaskAssignments on t.Id equals a.TaskId
                                     where t.TenantId == tenantId && a.UserId == userId && a.EndedAt == null
                                        && t.ParentTaskId == null
                                        && (t.Status == TaskState.Assigned || t.Status == TaskState.Accepted || t.Status == TaskState.InProgress || t.Status == TaskState.Submitted)
                                        && t.DeletedAt == null
                                     select t.Id).CountAsync(cancellationToken);

        var activeChildTasks = await (from t in db.WorkTasks
                                      join a in db.TaskAssignments on t.Id equals a.TaskId
                                      where t.TenantId == tenantId && a.UserId == userId && a.EndedAt == null
                                         && t.ParentTaskId != null
                                         && (t.Status == TaskState.Assigned || t.Status == TaskState.Accepted || t.Status == TaskState.InProgress || t.Status == TaskState.Submitted)
                                         && t.DeletedAt == null
                                      select t.Id).CountAsync(cancellationToken);

        var pendingApprovals = await (from r in db.Requests
                                      where r.TenantId == tenantId && r.Status == BizFlow.Domain.Requests.RequestState.Submitted && r.DeletedAt == null
                                      select r.Id).CountAsync(cancellationToken);

        bool isSoleManager = false;
        if (user.DepartmentId is { } deptId)
        {
            var otherManagersInDept = await (from u in db.Users
                                             join ur in db.UserRoles on u.Id equals ur.UserId
                                             join r in db.Roles on ur.RoleId equals r.Id
                                             where u.TenantId == tenantId && u.DepartmentId == deptId && u.Id != userId
                                                && u.Status == UserStatus.Active && u.DeletedAt == null
                                                && r.Name == "MANAGER"
                                             select u.Id).AnyAsync(cancellationToken);
            isSoleManager = !otherManagersInDept;
        }

        var replacements = await (from u in db.Users
                                  join ur in db.UserRoles on u.Id equals ur.UserId
                                  join r in db.Roles on ur.RoleId equals r.Id
                                  where u.TenantId == tenantId && u.Id != userId
                                     && u.Status == UserStatus.Active && u.DeletedAt == null
                                     && r.Name == "MANAGER"
                                  select new EligibleManagerView(u.Id, u.FullName, u.EmployeeCode))
                                  .Distinct().ToListAsync(cancellationToken);

        return new UserResponsibilitiesView(user.Id, user.FullName, user.EmployeeCode, user.DepartmentId, deptName,
            activeMainTasks, activeChildTasks, pendingApprovals, isSoleManager, replacements);
    }

    public async Task<UserOperationResult> PromoteToManagerAsync(Guid tenantId, Guid userId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (user is null) throw ApplicationFault.NotFound();

        if (user.Status != UserStatus.Active)
            throw new ApplicationFault(FaultKind.Validation, "USER.INACTIVE", "Only active employees can be promoted to Manager.");

        if (user.DepartmentId is null)
            throw new ApplicationFault(FaultKind.Validation, "USER.DEPARTMENT_REQUIRED", "The employee must be assigned to a department before being promoted to Manager.");

        var managerRole = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.Name == "MANAGER" && r.IsSystem, cancellationToken)
            ?? throw new InvalidOperationException("System role MANAGER not found.");

        var alreadyHasRole = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == managerRole.Id, cancellationToken);
        if (!alreadyHasRole)
        {
            db.UserRoles.Add(new UserRole(userId, managerRole.Id));
        }

        var hasScope = await db.ManagementScopes.AnyAsync(ms => ms.TenantId == tenantId && ms.UserId == userId && ms.DepartmentId == user.DepartmentId.Value, cancellationToken);
        if (!hasScope)
        {
            db.ManagementScopes.Add(ManagementScope.Create(tenantId, userId, user.DepartmentId.Value, true, actorId, now));
        }

        db.AuditLogs.Add(AuditLog.UserUpdated(tenantId, actorId, userId, now));
        await db.SaveChangesAsync(cancellationToken);

        var assignedRoleIds = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);
        return new(user.Id, user.EmployeeCode, user.Email, user.FullName, user.DepartmentId, user.Status.ToString().ToUpperInvariant(), assignedRoleIds, user.CreatedAt);
    }

    public async Task<UserOperationResult> DemoteToEmployeeAsync(Guid tenantId, Guid userId, Guid? transferToUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (user is null) throw ApplicationFault.NotFound();

        var activeAssignments = await db.TaskAssignments
            .Where(a => a.UserId == userId && a.EndedAt == null)
            .Join(db.WorkTasks.Where(t => t.TenantId == tenantId && (t.Status == TaskState.Assigned || t.Status == TaskState.Accepted || t.Status == TaskState.InProgress || t.Status == TaskState.Submitted) && t.DeletedAt == null),
                  a => a.TaskId, t => t.Id, (a, t) => new { Assignment = a, Task = t })
            .ToListAsync(cancellationToken);

        if (activeAssignments.Count > 0)
        {
            if (transferToUserId is null)
            {
                throw new ApplicationFault(FaultKind.Conflict, "USER.OUTSTANDING_RESPONSIBILITIES",
                    $"This manager has {activeAssignments.Count} active task assignment(s). Please transfer them to another eligible Manager before demoting.");
            }

            var replacementManager = await db.Users.SingleOrDefaultAsync(u => u.Id == transferToUserId.Value && u.TenantId == tenantId && u.Status == UserStatus.Active && u.DeletedAt == null, cancellationToken);
            if (replacementManager is null)
                throw new ApplicationFault(FaultKind.Validation, "USER.INVALID_REPLACEMENT", "The designated replacement manager does not exist or is inactive.");

            var isReplacementManager = await (from ur in db.UserRoles
                                              join r in db.Roles on ur.RoleId equals r.Id
                                              where ur.UserId == replacementManager.Id && r.Name == "MANAGER"
                                              select ur).AnyAsync(cancellationToken);
            if (!isReplacementManager)
                throw new ApplicationFault(FaultKind.Validation, "USER.REPLACEMENT_NOT_MANAGER", "The designated replacement must hold the Manager role.");

            foreach (var item in activeAssignments)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"TaskAssignment\" SET \"EndedAt\"={now} WHERE \"TaskAssignmentId\"={item.Assignment.Id} AND \"EndedAt\" IS NULL", cancellationToken);
                var newAssignment = TaskAssignment.Create(item.Task.Id, actorId, item.Task.ParentTaskId == null ? replacementManager.DepartmentId : null, replacementManager.Id, now);
                db.TaskAssignments.Add(newAssignment);
            }
        }

        var managerRole = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.Name == "MANAGER" && r.IsSystem, cancellationToken);
        if (managerRole is not null)
        {
            var managerLink = await db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == managerRole.Id, cancellationToken);
            if (managerLink is not null) db.UserRoles.Remove(managerLink);
        }

        var employeeRole = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.Name == "EMPLOYEE" && r.IsSystem, cancellationToken);
        if (employeeRole is not null)
        {
            var hasEmpRole = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == employeeRole.Id, cancellationToken);
            if (!hasEmpRole) db.UserRoles.Add(new UserRole(userId, employeeRole.Id));
        }

        var scopes = await db.ManagementScopes.Where(ms => ms.TenantId == tenantId && ms.UserId == userId).ToListAsync(cancellationToken);
        if (scopes.Count > 0) db.ManagementScopes.RemoveRange(scopes);

        db.AuditLogs.Add(AuditLog.UserUpdated(tenantId, actorId, userId, now));
        await db.SaveChangesAsync(cancellationToken);

        var assignedRoleIds = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);
        return new(user.Id, user.EmployeeCode, user.Email, user.FullName, user.DepartmentId, user.Status.ToString().ToUpperInvariant(), assignedRoleIds, user.CreatedAt);
    }

    public async Task<int> CountActiveAdminsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await (from u in db.Users
                      join ur in db.UserRoles on u.Id equals ur.UserId
                      join r in db.Roles on ur.RoleId equals r.Id
                      where u.TenantId == tenantId
                         && u.DeletedAt == null
                         && u.Status == UserStatus.Active
                         && (r.Name == "COMPANY_ADMIN" || r.Id == Guid.Parse("019f7f8a-0000-7000-8000-000000000002"))
                      select u.Id)
                      .Distinct()
                      .CountAsync(cancellationToken);
    }

    public async Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        return await (from ur in db.UserRoles
                      join r in db.Roles on ur.RoleId equals r.Id
                      where ur.UserId == userId
                         && (r.Name == "COMPANY_ADMIN" || r.Id == Guid.Parse("019f7f8a-0000-7000-8000-000000000002"))
                      select ur.UserId)
                      .AnyAsync(cancellationToken);
    }

    public async Task<bool> HasAdminRoleAsync(Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds == null || roleIds.Count == 0) return false;
        return await db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .AnyAsync(r => r.Name == "COMPANY_ADMIN" || r.Id == Guid.Parse("019f7f8a-0000-7000-8000-000000000002"), cancellationToken);
    }

    public async Task<UserOperationResult> TransferAdminOwnershipAsync(
        Guid tenantId,
        Guid currentOwnerId,
        Guid targetUserId,
        Guid? demoteToRoleId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var targetUser = await db.Users.SingleOrDefaultAsync(u => u.Id == targetUserId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (targetUser is null) throw ApplicationFault.NotFound();

        if (targetUser.Status != UserStatus.Active)
            throw new ApplicationFault(FaultKind.Validation, "USER.INACTIVE", "Target user must be an active employee in the company.");

        var adminRole = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.Name == "COMPANY_ADMIN" && r.IsSystem, cancellationToken)
            ?? throw new InvalidOperationException("System role COMPANY_ADMIN not found.");

        // 1. Assign admin role to target user if not already present
        var targetHasAdmin = await db.UserRoles.AnyAsync(ur => ur.UserId == targetUserId && ur.RoleId == adminRole.Id, cancellationToken);
        if (!targetHasAdmin)
        {
            db.UserRoles.Add(new UserRole(targetUserId, adminRole.Id));
        }

        // 2. Demote current owner if requested or by default
        if (demoteToRoleId is { } newRoleId)
        {
            var adminLink = await db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == currentOwnerId && ur.RoleId == adminRole.Id, cancellationToken);
            if (adminLink is not null) db.UserRoles.Remove(adminLink);

            var hasNewRole = await db.UserRoles.AnyAsync(ur => ur.UserId == currentOwnerId && ur.RoleId == newRoleId, cancellationToken);
            if (!hasNewRole)
            {
                db.UserRoles.Add(new UserRole(currentOwnerId, newRoleId));
            }
        }
        else
        {
            var adminLink = await db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == currentOwnerId && ur.RoleId == adminRole.Id, cancellationToken);
            if (adminLink is not null) db.UserRoles.Remove(adminLink);

            var managerRole = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Name == "MANAGER" && r.IsSystem, cancellationToken);
            var defaultRole = managerRole ?? await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Name == "EMPLOYEE" && r.IsSystem, cancellationToken);
            if (defaultRole != null)
            {
                var hasRole = await db.UserRoles.AnyAsync(ur => ur.UserId == currentOwnerId && ur.RoleId == defaultRole.Id, cancellationToken);
                if (!hasRole) db.UserRoles.Add(new UserRole(currentOwnerId, defaultRole.Id));
            }
        }

        db.AuditLogs.Add(AuditLog.OwnershipTransferred(tenantId, currentOwnerId, targetUserId, now));
        await db.SaveChangesAsync(cancellationToken);

        var assignedRoleIds = await db.UserRoles.Where(ur => ur.UserId == targetUserId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);
        return new(targetUser.Id, targetUser.EmployeeCode, targetUser.Email, targetUser.FullName, targetUser.DepartmentId, targetUser.Status.ToString().ToUpperInvariant(), assignedRoleIds, targetUser.CreatedAt);
    }
}
