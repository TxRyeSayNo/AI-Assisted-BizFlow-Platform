using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Authentication;

public sealed class AccessSnapshotProvider(BizFlowDbContext db, TimeProvider clock) : IAccessSnapshotProvider
{
    public async Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
    {
        // This narrowly scoped identity lookup is shared by the authorizer and, after credential
        // verification, authentication. It never returns other users or trusts token-carried grants.
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && x.DeletedAt == null, cancellationToken);
        if (user is null || (tenantId is null && !user.IsPlatformAdministrator)) return null;

        var tenantActive = tenantId is null || await db.Tenants.IgnoreQueryFilters()
            .Join(db.Companies.IgnoreQueryFilters(), t => t.CompanyId, c => c.Id, (t, c) => new { Tenant = t, Company = c })
            .AnyAsync(x => x.Tenant.Id == tenantId && x.Tenant.Status == TenantStatus.Active &&
                x.Company.Status == CompanyStatus.Active, cancellationToken);

        var grants = await (from ur in db.UserRoles.IgnoreQueryFilters()
                            join role in db.Roles.IgnoreQueryFilters() on ur.RoleId equals role.Id
                            join rp in db.RolePermissions.IgnoreQueryFilters() on role.Id equals rp.RoleId
                            join p in db.Permissions.IgnoreQueryFilters() on rp.PermissionId equals p.Id
                            where ur.UserId == userId && role.Status == RecordStatus.Active &&
                                (role.IsSystem || role.TenantId == tenantId) &&
                                (tenantId == null ? p.ScopeType == PermissionScope.Platform : p.ScopeType != PermissionScope.Platform)
                            select new PermissionGrant(p.Code, p.ScopeType)).Distinct().ToListAsync(cancellationToken);

        var departments = new HashSet<Guid>();
        if (tenantId is not null && user.DepartmentId is { } department &&
            await db.Departments.IgnoreQueryFilters().AnyAsync(x => x.Id == department && x.TenantId == tenantId && x.Status == RecordStatus.Active, cancellationToken))
            departments.Add(department);

        IReadOnlySet<Guid> managedDepartments = new HashSet<Guid>();
        if (tenantId is { } scopeTenant)
        {
            // Authentication uses this resolver before an HTTP tenant context exists. Every
            // bypass therefore carries both the verified tenant and exact subject predicate.
            var roots = await db.ManagementScopes.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == scopeTenant && x.UserId == user.Id)
                .Select(x => new ManagementScopeRoot(x.DepartmentId, x.IncludeDescendants))
                .ToListAsync(cancellationToken);
            if (roots.Count != 0)
            {
                var hierarchy = await db.Departments.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.TenantId == scopeTenant)
                    .Select(x => new DepartmentHierarchyNode(x.Id, x.ParentDepartmentId))
                    .ToListAsync(cancellationToken);
                managedDepartments = ManagementScopeExpansion.Expand(hierarchy, roots);
            }
        }

        return new AccessSnapshot(user.Id, user.TenantId,
            user.CanAuthenticate(clock.GetUtcNow()),
            tenantActive, grants, departments, managedDepartments);
    }
}
