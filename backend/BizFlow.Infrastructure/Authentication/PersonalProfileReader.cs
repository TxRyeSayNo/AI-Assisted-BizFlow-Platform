using BizFlow.Application.Authentication;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Authentication;

public sealed class PersonalProfileReader(BizFlowDbContext db) : IPersonalProfileReader
{
    public async Task<PersonalProfile?> ReadAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
    {
        // Explicit identity + account plane restriction also covers platform accounts.
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
            u => u.Id == userId && u.TenantId == tenantId && u.DeletedAt == null, cancellationToken);
        if (user is null) return null;
        var tenant = tenantId is { } tid ? await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tid, cancellationToken) : null;
        var department = user.DepartmentId is { } did ? await db.Departments.AsNoTracking()
            .Where(d => d.Id == did && d.TenantId == tenantId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken) : null;
        var roles = await (from ur in db.UserRoles.IgnoreQueryFilters()
                           join r in db.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                           where ur.UserId == user.Id && (r.TenantId == tenantId || r.IsSystem)
                           select r.Name).Distinct().OrderBy(n => n).ToArrayAsync(cancellationToken);
        return new(user.Id, user.FullName, user.EmployeeCode, user.Email, department,
            tenant?.Name, tenant?.TenantKey, roles, user.AvatarObjectKey is not null, user.CreatedAt, Nickname: user.Nickname ?? user.FullName);
    }
}
