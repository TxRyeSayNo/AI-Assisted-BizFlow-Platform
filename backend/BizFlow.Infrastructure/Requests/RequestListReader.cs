using BizFlow.Application.Requests;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Requests;

internal static class RequestReadQueries
{
    internal static IQueryable<WorkRequest> Visible(BizFlowDbContext db, RequestReadScope scope)
    {
        var managed = scope.ManagedDepartmentIds.ToArray();
        return db.Requests.AsNoTracking().Where(r => r.TenantId == scope.TenantId &&
            (scope.Tenant ||
             (scope.Own && r.RequesterId == scope.UserId) ||
             (scope.Managed && db.Users.Any(u => u.TenantId == scope.TenantId && u.Id == r.RequesterId &&
                 u.DepartmentId != null && managed.Contains(u.DepartmentId.Value)))));
    }
}

public sealed class RequestListReader(BizFlowDbContext db) : IRequestListReader
{
    public async Task<RequestListPage> ListAsync(RequestReadScope scope, RequestListFilter filter, CancellationToken cancellationToken)
    {
        var query = RequestReadQueries.Visible(db, scope);

        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(r => r.Title.ToUpper().Contains(normalized));
        }

        if (filter.Status is { } status)
        {
            var state = Enum.GetValues<RequestState>().Single(s => RequestListCodes.State(s) == status);
            query = query.Where(r => r.Status == state);
        }

        if (filter.Priority is { } priority)
        {
            var value = Enum.GetValues<RequestPriority>().Single(p => RequestListCodes.Priority(p) == priority);
            query = query.Where(r => r.Priority == value);
        }

        if (filter.ServiceId is { } serviceId)
        {
            query = query.Where(r => r.ServiceId == serviceId);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(r => r.CategoryId == categoryId);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Status,
                r.Priority,
                r.ServiceId,
                ServiceName = db.Services.Where(s => s.TenantId == scope.TenantId && s.Id == r.ServiceId).Select(s => s.Name).FirstOrDefault(),
                r.CategoryId,
                CategoryName = db.ServiceCategories.Where(c => c.Id == r.CategoryId).Select(c => c.Name).FirstOrDefault(),
                r.RequesterId,
                RequesterName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == r.RequesterId).Select(u => u.FullName).FirstOrDefault(),
                r.CreatedAt,
                r.UpdatedAt
            }).ToListAsync(cancellationToken);

        return new(
            rows.Select(r => new RequestListRow(
                r.Id,
                r.Title,
                RequestListCodes.State(r.Status),
                RequestListCodes.Priority(r.Priority),
                r.ServiceId,
                r.ServiceName,
                r.CategoryId,
                r.CategoryName,
                r.RequesterId,
                r.RequesterName,
                r.CreatedAt,
                r.UpdatedAt)).ToArray(),
            filter.Page,
            filter.PageSize,
            total);
    }
}
