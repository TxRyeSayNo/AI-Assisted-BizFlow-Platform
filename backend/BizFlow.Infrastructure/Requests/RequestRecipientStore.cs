using BizFlow.Application.Requests;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestRecipientStore(BizFlowDbContext db) : IRequestRecipientStore
{
    private const string ElevatedCode = "requests.approve.elevated";
    private const string RouteCode = "requests.route";

    public async Task<int> GetUserLevelAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var codes = await (from ur in db.UserRoles
                           join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                           join p in db.Permissions on rp.PermissionId equals p.Id
                           where ur.UserId == userId && (p.Code == ElevatedCode || p.Code == RouteCode)
                           select p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
        return codes.Contains(ElevatedCode) ? 2 : codes.Contains(RouteCode) ? 1 : 0;
    }

    public async Task<RecipientOptions> ListAllowedAsync(Guid tenantId, int myLevel, Guid excludeUserId, CancellationToken cancellationToken)
    {
        var maxLevel = myLevel + 1;

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
            .OrderBy(d => d.Name)
            .Select(d => new RecipientDepartmentOption(d.Id, d.Name))
            .ToListAsync(cancellationToken);

        var candidates = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Status == UserStatus.Active && u.DeletedAt == null && u.Id != excludeUserId)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.EmployeeCode,
                HasElevated = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Any(ur => db.RolePermissions.Where(rp => rp.RoleId == ur.RoleId)
                        .Any(rp => db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == ElevatedCode))),
                HasRoute = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Any(ur => db.RolePermissions.Where(rp => rp.RoleId == ur.RoleId)
                        .Any(rp => db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == RouteCode)))
            })
            .ToListAsync(cancellationToken);

        var users = candidates
            .Select(u => new RecipientUserOption(u.Id, u.FullName, u.EmployeeCode, u.HasElevated ? 2 : u.HasRoute ? 1 : 0))
            .Where(u => u.Level <= maxLevel)
            .OrderBy(u => u.Level).ThenBy(u => u.FullName)
            .ToList();

        return new RecipientOptions(myLevel, departments, users);
    }
}
