using BizFlow.Application.Organization;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Organization;

public sealed class DepartmentReader(BizFlowDbContext db) : IDepartmentReader
{
    public async Task<DepartmentPage> ListAsync(Guid tenantId, DepartmentFilter filter, CancellationToken cancellationToken)
    {
        // The explicit tenant predicate is additional to, never a replacement for, default filters.
        var query = db.Departments.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (filter.Status is { } status) query = query.Where(d => d.Status == Enum.Parse<RecordStatus>(status, true));
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(d => d.Code.Contains(normalized) || d.Name.ToUpper().Contains(normalized));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(d => new { d.Id, d.Code, d.Name, d.ParentDepartmentId, d.Status, d.CreatedAt }).ToListAsync(cancellationToken);
        return new(rows.Select(d => new DepartmentRow(d.Id, d.Code, d.Name, d.ParentDepartmentId,
            d.Status.ToString().ToUpperInvariant(), d.CreatedAt)).ToArray(), filter.Page, filter.PageSize, total);
    }
}
