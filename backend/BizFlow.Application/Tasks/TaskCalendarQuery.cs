using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

/// <summary>
/// Read model for the deadline calendar (F2). A calendar needs a flat window of dated work, which
/// neither the paged list nor the per-status board can express without truncating a busy month, so
/// this is its own read. It is additive and read-only, gated by the same
/// <see cref="TaskReadPolicy"/>, and returns only tasks that actually carry a deadline.
/// </summary>
public sealed record TaskCalendarFilter(
    DateTimeOffset From,
    DateTimeOffset To,
    string? Status = null,
    string? Priority = null,
    Guid? AssignedUserId = null,
    Guid? AssignedDepartmentId = null,
    int Limit = 500);

/// <summary><paramref name="Truncated"/> tells the client the window held more than
/// <paramref name="Limit"/> dated tasks, so it can ask for a narrower range instead of silently
/// showing an incomplete month.</summary>
public sealed record TaskCalendarView(
    IReadOnlyList<TaskListRow> Items,
    DateTimeOffset From,
    DateTimeOffset To,
    int Limit,
    bool Truncated);

public interface ITaskCalendarReader
{
    Task<(IReadOnlyList<TaskListRow> Items, bool Truncated)> ReadAsync(TaskReadScope scope,
        TaskCalendarFilter filter, CancellationToken cancellationToken);
}

public sealed class TaskCalendarQuery(ITenantContext context, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit, ITaskCalendarReader reader)
{
    public const int MaxLimit = 1000;
    public const int MaxWindowDays = 366;

    public async Task<TaskCalendarView> GetAsync(TaskCalendarFilter filter, CancellationToken cancellationToken)
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
        filter = filter with { Status = Clean(filter.Status), Priority = Clean(filter.Priority) };
        if (filter.Limit is < 1 or > MaxLimit ||
            filter.From > filter.To ||
            (filter.To - filter.From).TotalDays > MaxWindowDays ||
            (filter.Status is not null &&
                !Enum.GetValues<TaskState>().Any(s => TaskListCodes.State(s) == filter.Status)) ||
            (filter.Priority is not null &&
                !Enum.GetValues<TaskPriority>().Any(p => TaskListCodes.Priority(p) == filter.Priority)) ||
            filter.AssignedUserId == Guid.Empty || filter.AssignedDepartmentId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED",
                $"Use a valid status, priority, assignee, a window of at most {MaxWindowDays} days and a limit from 1 to {MaxLimit}.");
        var (items, truncated) = await reader.ReadAsync(scope, filter, cancellationToken);
        return new(items, filter.From, filter.To, filter.Limit, truncated);
    }
}
