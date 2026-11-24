using BizFlow.Application.Requests;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Requests;

/// <summary>
/// Groups the visible requests into one lane per lifecycle state. Starts from the same
/// <see cref="RequestReadQueries.Visible"/> gate the list reader uses, so it inherits the resource
/// scope and the fail-closed tenant query filter on <see cref="WorkRequest"/> without widening
/// either, and never calls IgnoreQueryFilters.
/// </summary>
public sealed class RequestBoardReader(BizFlowDbContext db) : IRequestBoardReader
{
    public async Task<IReadOnlyList<RequestBoardColumn>> ReadAsync(RequestReadScope scope, RequestBoardFilter filter,
        CancellationToken cancellationToken)
    {
        var narrowed = Narrow(RequestReadQueries.Visible(db, scope), filter);
        var totals = await narrowed.GroupBy(r => r.Status)
            .Select(group => new { Status = group.Key, Total = group.LongCount() })
            .ToListAsync(cancellationToken);
        var columns = new List<RequestBoardColumn>();
        foreach (var state in Enum.GetValues<RequestState>())
        {
            var total = totals.SingleOrDefault(candidate => candidate.Status == state)?.Total ?? 0;
            if (total == 0)
            {
                columns.Add(new(RequestListCodes.State(state), [], 0));
                continue;
            }
            var rows = await narrowed.Where(r => r.Status == state)
                .OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id)
                .Take(filter.ColumnSize)
                .Select(r => new
                {
                    r.Id, r.Title, r.Status, r.Priority, r.ServiceId,
                    ServiceName = db.Services.Where(s => s.TenantId == scope.TenantId && s.Id == r.ServiceId).Select(s => s.Name).FirstOrDefault(),
                    r.CategoryId,
                    CategoryName = db.ServiceCategories.Where(c => c.Id == r.CategoryId).Select(c => c.Name).FirstOrDefault(),
                    r.RequesterId,
                    RequesterName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == r.RequesterId).Select(u => u.FullName).FirstOrDefault(),
                    r.CreatedAt, r.UpdatedAt
                }).ToListAsync(cancellationToken);
            columns.Add(new(RequestListCodes.State(state),
                rows.Select(r => new RequestListRow(r.Id, r.Title, RequestListCodes.State(r.Status),
                    RequestListCodes.Priority(r.Priority), r.ServiceId, r.ServiceName, r.CategoryId, r.CategoryName,
                    r.RequesterId, r.RequesterName, r.CreatedAt, r.UpdatedAt)).ToArray(),
                total));
        }
        return columns;
    }

    private static IQueryable<WorkRequest> Narrow(IQueryable<WorkRequest> query, RequestBoardFilter filter)
    {
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(r => r.Title.ToUpper().Contains(normalized));
        }
        if (filter.Priority is { } priority)
        {
            var value = Enum.GetValues<RequestPriority>().Single(p => RequestListCodes.Priority(p) == priority);
            query = query.Where(r => r.Priority == value);
        }
        if (filter.ServiceId is { } serviceId) query = query.Where(r => r.ServiceId == serviceId);
        if (filter.CategoryId is { } categoryId) query = query.Where(r => r.CategoryId == categoryId);
        if (filter.RequesterId is { } requesterId) query = query.Where(r => r.RequesterId == requesterId);
        return query;
    }
}
