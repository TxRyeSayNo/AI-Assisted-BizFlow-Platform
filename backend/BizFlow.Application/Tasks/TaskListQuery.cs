using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

public sealed record TaskListFilter(int Page = 1, int PageSize = 25, string? Search = null,
    string? Status = null, string? Priority = null, Guid? RequestId = null);
public sealed record TaskListRow(Guid TaskId, string Title, string Status, string Priority,
    DateTimeOffset? Deadline, DateTimeOffset CreatedAt, Guid CreatorId, Guid? RequestId,
    Guid? AssignedUserId, string? AssignedUserName, Guid? AssignedDepartmentId, string? AssignedDepartmentName);
public sealed record TaskListPage(IReadOnlyList<TaskListRow> Items, int Page, int PageSize, long Total);
public interface ITaskListReader
{
    Task<TaskListPage> ListAsync(TaskReadScope scope, TaskListFilter filter, CancellationToken cancellationToken);
}

public static class TaskListCodes
{
    public static string State(TaskState state) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(state.ToString());
    public static string Priority(TaskPriority priority) => priority.ToString().ToUpperInvariant();
}

// API-TASK-01. Resolve all grants once from live storage, then apply their union before counting/paging.
public sealed class TaskListQuery(ITenantContext context, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit, ITaskListReader reader)
{
    public async Task<TaskListPage> ListAsync(TaskListFilter filter, CancellationToken cancellationToken)
    {
        var access = context.UserId is { } user && user != Guid.Empty
            ? await snapshots.ResolveAsync(user, context.TenantId, cancellationToken) : null;
        var (decision, scope) = access is null || access.UserId != context.UserId || access.TenantId != context.TenantId
            ? (AccessDecision.Deny(AccessDenial.Unauthenticated), (TaskReadScope?)null)
            : TaskReadPolicy.Resolve(access);
        if (!decision.Allowed || scope is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, "tasks.read", decision.Denial, cancellationToken);
            throw decision.Denial == AccessDenial.Unauthenticated
                ? new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.")
                : new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have access to tasks.");
        }
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        filter = filter with { Search = Clean(filter.Search), Status = Clean(filter.Status), Priority = Clean(filter.Priority) };
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            filter.Search?.Length > 200 ||
            (filter.Status is not null && !Enum.GetValues<TaskState>().Any(s => TaskListCodes.State(s) == filter.Status)) ||
            (filter.Priority is not null && !Enum.GetValues<TaskPriority>().Any(p => TaskListCodes.Priority(p) == filter.Priority)))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid task status, priority, search and page size from 1 to 100.");
        return await reader.ListAsync(scope, filter, cancellationToken);
    }
}
