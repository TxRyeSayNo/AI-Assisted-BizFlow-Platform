using BizFlow.Application.Organization;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Organization;

public sealed class UserDirectoryReader(BizFlowDbContext db) : IUserDirectoryReader
{
    public async Task<UserDirectoryPage> ListAsync(Guid tenantId, UserDirectoryFilter filter, CancellationToken cancellationToken)
    {
        // Keep the default user/department tenant filters, including soft-delete exclusion.
        var query = db.Users.AsNoTracking().Where(u => u.TenantId == tenantId);
        if (filter.Status is { } status) query = query.Where(u => u.Status == Enum.Parse<UserStatus>(status, true));
        if (filter.DepartmentId is { } department) query = query.Where(u => u.DepartmentId == department);
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmployeeCode.Contains(normalized) || u.FullName.ToUpper().Contains(normalized) || u.NormalizedEmail.Contains(normalized));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(u => new
            {
                u.Id, u.EmployeeCode, u.FullName, u.Email, u.DepartmentId, u.Status,
                DepartmentName = db.Departments.Where(d => d.TenantId == tenantId && d.Id == u.DepartmentId).Select(d => d.Name).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return new(rows.Select(u => new UserDirectoryRow(u.Id, u.EmployeeCode, u.FullName, u.Email,
            u.DepartmentId, u.DepartmentName, u.Status.ToString().ToUpperInvariant())).ToArray(), filter.Page, filter.PageSize, total);
    }
}
