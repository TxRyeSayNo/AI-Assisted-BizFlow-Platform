using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Collaboration;

public sealed record RecordArchivalResult(
    Guid RecordId,
    string RecordType,
    string Status,
    DateTimeOffset ArchivedAt,
    string Message);

public sealed record RecordSearchQuery(
    string? Query = null,
    string? Type = null,
    string? Status = null,
    string? Priority = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    bool IncludeArchived = false,
    int Page = 1,
    int PageSize = 20);

public sealed record RecordSearchResultItem(
    Guid Id,
    string RecordType,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? CreatorOrRequesterId,
    string? CreatorOrRequesterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    bool IsArchived);

public sealed record RecordSearchPagedResult(
    IReadOnlyList<RecordSearchResultItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public interface IRecordArchivalStore
{
    Task<WorkTask?> FindTaskForArchivalAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken);
    Task<bool> IsTaskArchivedAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken);
    Task ArchiveTaskAsync(WorkTask task, AuditLog audit, CancellationToken cancellationToken);

    Task<WorkRequest?> FindRequestForArchivalAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken);
    Task<bool> IsRequestArchivedAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken);
    Task ArchiveRequestAsync(WorkRequest request, AuditLog audit, CancellationToken cancellationToken);
}

public interface IRecordSearchReader
{
    Task<RecordSearchPagedResult> SearchRecordsAsync(
        Guid tenantId,
        RecordSearchQuery query,
        CancellationToken cancellationToken);
}

public sealed class RecordService(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IRecordArchivalStore archivalStore,
    IRecordSearchReader searchReader)
{
    public async Task<RecordArchivalResult> ArchiveRecordAsync(
        string recordType,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("records.archive", new(tenantId), cancellationToken: cancellationToken);

        if (recordId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "RECORD.ID_REQUIRED", "Record ID is required.");

        if (string.IsNullOrWhiteSpace(recordType))
            throw new ApplicationFault(FaultKind.Validation, "RECORD.TYPE_REQUIRED", "Record type is required.");

        var normalizedType = recordType.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        switch (normalizedType)
        {
            case "TASK":
            {
                if (await archivalStore.IsTaskArchivedAsync(tenantId, recordId, cancellationToken))
                    throw new ApplicationFault(FaultKind.Conflict, "RECORD.ALREADY_ARCHIVED", "Task is already archived.");

                var task = await archivalStore.FindTaskForArchivalAsync(tenantId, recordId, cancellationToken);
                if (task is null)
                    throw new ApplicationFault(FaultKind.NotFound, "TASK.NOT_FOUND", "Task was not found.");

                if (task.Status is not (TaskState.Completed or TaskState.Cancelled))
                    throw new ApplicationFault(FaultKind.Conflict, "RECORD.CANNOT_ARCHIVE",
                        $"Only completed or cancelled tasks can be archived. Current state: {task.Status}.");

                task.Archive(now);
                var audit = AuditLog.RecordArchived(task, actorId, now);
                await archivalStore.ArchiveTaskAsync(task, audit, cancellationToken);

                return new(task.Id, "TASK", task.Status.ToString().ToUpperInvariant(), task.DeletedAt!.Value, "Task archived successfully.");
            }

            case "REQUEST":
            {
                if (await archivalStore.IsRequestArchivedAsync(tenantId, recordId, cancellationToken))
                    throw new ApplicationFault(FaultKind.Conflict, "RECORD.ALREADY_ARCHIVED", "Request is already archived.");

                var request = await archivalStore.FindRequestForArchivalAsync(tenantId, recordId, cancellationToken);
                if (request is null)
                    throw new ApplicationFault(FaultKind.NotFound, "REQUEST.NOT_FOUND", "Request was not found.");

                if (request.Status is not (RequestState.Closed or RequestState.Cancelled))
                    throw new ApplicationFault(FaultKind.Conflict, "RECORD.CANNOT_ARCHIVE",
                        $"Only closed or cancelled requests can be archived. Current state: {request.Status}.");

                request.Archive(now);
                var audit = AuditLog.RecordArchived(request, actorId, now);
                await archivalStore.ArchiveRequestAsync(request, audit, cancellationToken);

                return new(request.Id, "REQUEST", request.Status.ToString().ToUpperInvariant(), request.DeletedAt!.Value, "Request archived successfully.");
            }

            default:
                throw new ApplicationFault(FaultKind.Validation, "RECORD.INVALID_TYPE",
                    "Record type must be 'task' or 'request'.");
        }
    }

    public async Task<RecordSearchPagedResult> SearchRecordsAsync(
        RecordSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("records.search", new(tenantId), cancellationToken: cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);

        var normalizedQuery = query with
        {
            Page = page,
            PageSize = pageSize,
            Query = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim(),
            Type = string.IsNullOrWhiteSpace(query.Type) ? "ALL" : query.Type.Trim().ToUpperInvariant(),
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim().ToUpperInvariant(),
            Priority = string.IsNullOrWhiteSpace(query.Priority) ? null : query.Priority.Trim().ToUpperInvariant()
        };

        return await searchReader.SearchRecordsAsync(tenantId, normalizedQuery, cancellationToken);
    }
}
