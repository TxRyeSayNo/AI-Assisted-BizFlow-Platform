using BizFlow.Application.Collaboration;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class RecordSearchReader(BizFlowDbContext db) : IRecordSearchReader
{
    public async Task<RecordSearchPagedResult> SearchRecordsAsync(
        Guid tenantId,
        RecordSearchQuery query,
        CancellationToken cancellationToken)
    {
        var includeArchived = query.IncludeArchived;
        var typeFilter = query.Type?.ToUpperInvariant() ?? "ALL";
        var hasQuery = !string.IsNullOrWhiteSpace(query.Query);
        var normalizedQuery = hasQuery ? query.Query!.Trim().ToUpperInvariant() : null;

        var hasStatus = !string.IsNullOrWhiteSpace(query.Status);
        var parsedTaskStatus = hasStatus && Enum.TryParse<TaskState>(query.Status, true, out var ts) ? (TaskState?)ts : null;
        var parsedRequestStatus = hasStatus && Enum.TryParse<RequestState>(query.Status, true, out var rs) ? (RequestState?)rs : null;

        var hasPriority = !string.IsNullOrWhiteSpace(query.Priority);
        var parsedTaskPriority = hasPriority && Enum.TryParse<TaskPriority>(query.Priority, true, out var tp) ? (TaskPriority?)tp : null;
        var parsedRequestPriority = hasPriority && Enum.TryParse<RequestPriority>(query.Priority, true, out var rp) ? (RequestPriority?)rp : null;

        var skip = (query.Page - 1) * query.PageSize;
        var take = query.PageSize;

        if (typeFilter == "TASK")
        {
            var taskQuery = BuildTaskQuery(tenantId, includeArchived, normalizedQuery, hasStatus, parsedTaskStatus, hasPriority, parsedTaskPriority, query.From, query.To);
            var totalCount = await taskQuery.CountAsync(cancellationToken);
            if (totalCount == 0) return new(Array.Empty<RecordSearchResultItem>(), 0, query.Page, query.PageSize, 0);

            var rawTasks = await taskQuery
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            var actorIds = rawTasks.Select(t => t.CreatorId).Distinct().ToList();
            var userMap = await GetUserNamesAsync(tenantId, actorIds, cancellationToken);

            var items = rawTasks.Select(t => new RecordSearchResultItem(
                t.Id,
                "TASK",
                t.Title,
                t.Description,
                t.Status.ToString().ToUpperInvariant(),
                t.Priority.ToString().ToUpperInvariant(),
                t.CreatorId,
                userMap.GetValueOrDefault(t.CreatorId),
                t.CreatedAt,
                t.UpdatedAt,
                t.DeletedAt,
                t.DeletedAt != null
            )).ToList();

            var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);
            return new(items, totalCount, query.Page, query.PageSize, totalPages);
        }

        if (typeFilter == "REQUEST")
        {
            var requestQuery = BuildRequestQuery(tenantId, includeArchived, normalizedQuery, hasStatus, parsedRequestStatus, hasPriority, parsedRequestPriority, query.From, query.To);
            var totalCount = await requestQuery.CountAsync(cancellationToken);
            if (totalCount == 0) return new(Array.Empty<RecordSearchResultItem>(), 0, query.Page, query.PageSize, 0);

            var rawRequests = await requestQuery
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            var actorIds = rawRequests.Select(r => r.RequesterId).Distinct().ToList();
            var userMap = await GetUserNamesAsync(tenantId, actorIds, cancellationToken);

            var items = rawRequests.Select(r => new RecordSearchResultItem(
                r.Id,
                "REQUEST",
                r.Title,
                r.Description,
                r.Status.ToString().ToUpperInvariant(),
                r.Priority.ToString().ToUpperInvariant(),
                r.RequesterId,
                userMap.GetValueOrDefault(r.RequesterId),
                r.CreatedAt,
                r.UpdatedAt,
                r.DeletedAt,
                r.DeletedAt != null
            )).ToList();

            var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);
            return new(items, totalCount, query.Page, query.PageSize, totalPages);
        }

        // ALL
        var tasksAll = BuildTaskQuery(tenantId, includeArchived, normalizedQuery, hasStatus, parsedTaskStatus, hasPriority, parsedTaskPriority, query.From, query.To);
        var requestsAll = BuildRequestQuery(tenantId, includeArchived, normalizedQuery, hasStatus, parsedRequestStatus, hasPriority, parsedRequestPriority, query.From, query.To);

        var taskTotalCount = await tasksAll.CountAsync(cancellationToken);
        var requestTotalCount = await requestsAll.CountAsync(cancellationToken);
        var combinedTotal = taskTotalCount + requestTotalCount;

        if (combinedTotal == 0)
        {
            return new(Array.Empty<RecordSearchResultItem>(), 0, query.Page, query.PageSize, 0);
        }

        // To correctly paginate across both streams ordered by CreatedAt descending,
        // we take up to (skip + take) from each source, then merge, order, and slice.
        var fetchCap = Math.Min(skip + take, 1000);
        var taskBatch = await tasksAll
            .OrderByDescending(t => t.CreatedAt)
            .Take(fetchCap)
            .ToListAsync(cancellationToken);

        var requestBatch = await requestsAll
            .OrderByDescending(r => r.CreatedAt)
            .Take(fetchCap)
            .ToListAsync(cancellationToken);

        var mergedUnmapped = taskBatch.Select(t => new RecordSearchResultItem(
            t.Id,
            "TASK",
            t.Title,
            t.Description,
            t.Status.ToString().ToUpperInvariant(),
            t.Priority.ToString().ToUpperInvariant(),
            t.CreatorId,
            null,
            t.CreatedAt,
            t.UpdatedAt,
            t.DeletedAt,
            t.DeletedAt != null
        )).Concat(requestBatch.Select(r => new RecordSearchResultItem(
            r.Id,
            "REQUEST",
            r.Title,
            r.Description,
            r.Status.ToString().ToUpperInvariant(),
            r.Priority.ToString().ToUpperInvariant(),
            r.RequesterId,
            null,
            r.CreatedAt,
            r.UpdatedAt,
            r.DeletedAt,
            r.DeletedAt != null
        )))
        .OrderByDescending(x => x.CreatedAt)
        .Skip(skip)
        .Take(take)
        .ToList();

        var neededActorIds = mergedUnmapped
            .Where(x => x.CreatorOrRequesterId.HasValue)
            .Select(x => x.CreatorOrRequesterId!.Value)
            .Distinct()
            .ToList();

        var namesMap = await GetUserNamesAsync(tenantId, neededActorIds, cancellationToken);

        var finalItems = mergedUnmapped.Select(item => item with
        {
            CreatorOrRequesterName = item.CreatorOrRequesterId.HasValue
                ? namesMap.GetValueOrDefault(item.CreatorOrRequesterId.Value)
                : null
        }).ToList();

        var pages = (int)Math.Ceiling((double)combinedTotal / query.PageSize);
        return new(finalItems, combinedTotal, query.Page, query.PageSize, pages);
    }

    private IQueryable<WorkTask> BuildTaskQuery(
        Guid tenantId,
        bool includeArchived,
        string? normalizedQuery,
        bool hasStatus,
        TaskState? parsedTaskStatus,
        bool hasPriority,
        TaskPriority? parsedTaskPriority,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var tasks = db.WorkTasks.IgnoreQueryFilters().Where(t => t.TenantId == tenantId);
        if (!includeArchived)
        {
            tasks = tasks.Where(t => t.DeletedAt == null);
        }

        if (normalizedQuery is not null)
        {
            tasks = tasks.Where(t => t.Title.ToUpper().Contains(normalizedQuery) ||
                (t.Description != null && t.Description.ToUpper().Contains(normalizedQuery)));
        }

        if (hasStatus)
        {
            tasks = parsedTaskStatus.HasValue
                ? tasks.Where(t => t.Status == parsedTaskStatus.Value)
                : tasks.Where(_ => false);
        }

        if (hasPriority)
        {
            tasks = parsedTaskPriority.HasValue
                ? tasks.Where(t => t.Priority == parsedTaskPriority.Value)
                : tasks.Where(_ => false);
        }

        if (from.HasValue)
        {
            tasks = tasks.Where(t => t.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            tasks = tasks.Where(t => t.CreatedAt <= to.Value);
        }

        return tasks;
    }

    private IQueryable<WorkRequest> BuildRequestQuery(
        Guid tenantId,
        bool includeArchived,
        string? normalizedQuery,
        bool hasStatus,
        RequestState? parsedRequestStatus,
        bool hasPriority,
        RequestPriority? parsedRequestPriority,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var requests = db.Requests.IgnoreQueryFilters().Where(r => r.TenantId == tenantId);
        if (!includeArchived)
        {
            requests = requests.Where(r => r.DeletedAt == null);
        }

        if (normalizedQuery is not null)
        {
            requests = requests.Where(r => r.Title.ToUpper().Contains(normalizedQuery) ||
                r.Description.ToUpper().Contains(normalizedQuery));
        }

        if (hasStatus)
        {
            requests = parsedRequestStatus.HasValue
                ? requests.Where(r => r.Status == parsedRequestStatus.Value)
                : requests.Where(_ => false);
        }

        if (hasPriority)
        {
            requests = parsedRequestPriority.HasValue
                ? requests.Where(r => r.Priority == parsedRequestPriority.Value)
                : requests.Where(_ => false);
        }

        if (from.HasValue)
        {
            requests = requests.Where(r => r.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            requests = requests.Where(r => r.CreatedAt <= to.Value);
        }

        return requests;
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(
        Guid tenantId,
        IReadOnlyList<Guid> actorIds,
        CancellationToken cancellationToken)
    {
        if (actorIds.Count == 0) return new();

        return await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }
}
