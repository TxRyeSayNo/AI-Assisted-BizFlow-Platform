using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

/// <summary>
/// Board-shaped read model for the Kanban view. It is additive next to <see cref="TaskListQuery"/>:
/// the list query keeps its own filter contract untouched, while the board needs assignee,
/// department and deadline-range narrowing plus per-status grouping that paging cannot express.
/// Visibility is resolved by the very same <see cref="TaskReadPolicy"/> gate, so the board can
/// never widen what a caller is allowed to read.
/// </summary>
public sealed record TaskBoardFilter(
    string? Search = null,
    string? Priority = null,
    Guid? AssignedUserId = null,
    Guid? AssignedDepartmentId = null,
    DateTimeOffset? DeadlineFrom = null,
    DateTimeOffset? DeadlineTo = null,
    Guid? RequestId = null,
    bool? IsParentOnly = null,
    // F6: narrow the board to one label.
    Guid? LabelId = null,
    int ColumnSize = 50);

/// <summary>One lane of the board. <paramref name="Total"/> counts every visible task in that
/// status, so the client can show "showing 50 of 214" without a second request.</summary>
public sealed record TaskBoardColumn(string Status, IReadOnlyList<TaskListRow> Items, long Total);

public sealed record TaskBoardView(IReadOnlyList<TaskBoardColumn> Columns, int ColumnSize);

public interface ITaskBoardReader
{
    Task<IReadOnlyList<TaskBoardColumn>> ReadAsync(TaskReadScope scope, TaskBoardFilter filter,
        CancellationToken cancellationToken);
}

// Mirrors API-TASK-01: resolve every grant once from live storage, then apply their union before
// counting. The board never mutates a task; status changes go through the existing task commands.
public sealed class TaskBoardQuery(ITenantContext context, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit, ITaskBoardReader reader)
{
    public const int MaxColumnSize = 100;
    public const int MaxSearchLength = 200;

    public async Task<TaskBoardView> GetAsync(TaskBoardFilter filter, CancellationToken cancellationToken)
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
        filter = filter with { Search = Clean(filter.Search), Priority = Clean(filter.Priority) };
        if (filter.ColumnSize is < 1 or > MaxColumnSize ||
            filter.Search?.Length > MaxSearchLength ||
            (filter.Priority is not null &&
                !Enum.GetValues<TaskPriority>().Any(p => TaskListCodes.Priority(p) == filter.Priority)) ||
            (filter.AssignedUserId == Guid.Empty) || (filter.AssignedDepartmentId == Guid.Empty) ||
            (filter.RequestId == Guid.Empty) || (filter.LabelId == Guid.Empty) ||
            (filter.DeadlineFrom is { } from && filter.DeadlineTo is { } to && from > to))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED",
                "Use a valid task priority, search, assignee, deadline range and column size from 1 to 100.");
        var columns = await reader.ReadAsync(scope, filter, cancellationToken);
        return new(columns, filter.ColumnSize);
    }
}
