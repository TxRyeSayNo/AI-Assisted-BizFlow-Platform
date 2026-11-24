using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Dashboard;

/// <summary>
/// Shared read-scope gate for every dashboard query. It resolves the caller's live grants from
/// authoritative storage (never from the token), records denials for audit and fails closed.
/// This mirrors <see cref="TaskListQuery"/> so dashboards can never widen task visibility.
/// </summary>
internal static class DashboardAccess
{
    internal static async Task<TaskReadScope> RequireTaskReadScopeAsync(
        ITenantContext context,
        IAccessSnapshotProvider snapshots,
        ISecurityAuditWriter audit,
        string operation,
        CancellationToken cancellationToken)
    {
        var access = context.UserId is { } user && user != Guid.Empty
            ? await snapshots.ResolveAsync(user, context.TenantId, cancellationToken)
            : null;
        var (decision, scope) = access is null || access.UserId != context.UserId || access.TenantId != context.TenantId
            ? (AccessDecision.Deny(AccessDenial.Unauthenticated), (TaskReadScope?)null)
            : TaskReadPolicy.Resolve(access);

        if (decision.Allowed && scope is not null) return scope;

        await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, operation, decision.Denial, cancellationToken);
        throw decision.Denial == AccessDenial.Unauthenticated
            ? new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.")
            : new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have access to this dashboard.");
    }

    internal static void RequirePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED",
                "Use a page from 1 and a page size from 1 to 100.");
    }

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Maps server-resolved rows onto API shapes. All arithmetic stays in the Domain policy.</summary>
internal static class DashboardProjection
{
    internal static string Size(ProjectSizeCategory category) => category switch
    {
        ProjectSizeCategory.Large => "LARGE",
        ProjectSizeCategory.Small => "SMALL",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown project size category.")
    };

    internal static ProjectSummaryView Summary(ProjectHeaderData header,
        IReadOnlyCollection<WorkItemProgressInput> workItems, ProjectSizeThresholds thresholds)
    {
        var aggregate = ProjectDashboardPolicy.Aggregate(header.Status, workItems,
            thresholds.MinWorkItems, thresholds.MinParticipants);
        return new ProjectSummaryView(
            header.ProjectId,
            header.Title,
            TaskListCodes.State(header.Status),
            TaskListCodes.Priority(header.Priority),
            header.Deadline,
            header.OwnerUserId,
            header.OwnerName,
            header.OwnerDepartmentId,
            header.OwnerDepartmentName,
            Size(aggregate.SizeCategory),
            aggregate.ProgressPercent,
            aggregate.WorkItemCount,
            aggregate.CompletedWorkItemCount,
            aggregate.CancelledWorkItemCount,
            aggregate.OverdueWorkItemCount,
            aggregate.UnassignedWorkItemCount,
            aggregate.ParticipantCount,
            header.CreatedAt,
            header.UpdatedAt);
    }

    internal static ProjectWorkItemView WorkItem(ProjectWorkItemData data)
    {
        var progress = data.Progress;
        return new ProjectWorkItemView(
            progress.TaskId,
            data.Title,
            TaskListCodes.State(progress.Status),
            TaskListCodes.Priority(progress.Priority),
            data.Deadline,
            ProjectDashboardPolicy.Weight(progress.Priority),
            ProjectDashboardPolicy.WorkItemPercent(progress),
            progress.ChecklistTotal,
            progress.ChecklistDone,
            progress.AssigneeUserId,
            data.AssigneeName,
            progress.AssigneeDepartmentId,
            data.AssigneeDepartmentName,
            progress.IsOverdue,
            data.UpdatedAt);
    }

    /// <summary>
    /// Turns the weighted split into participant rows. Identity comes from the work items
    /// themselves, so no extra directory lookup is needed and no user outside the project
    /// can be named. The caller decides which rows may be disclosed.
    /// </summary>
    internal static IReadOnlyList<ProjectParticipantView> Participants(
        IReadOnlyList<ParticipantShare> shares, IReadOnlyList<ProjectWorkItemData> workItems, Guid selfUserId)
    {
        var views = new List<ProjectParticipantView>(shares.Count);
        foreach (var share in shares)
        {
            var rows = workItems.Where(x => share.UserId is { } user
                ? x.Progress.AssigneeUserId == user
                : x.Progress.AssigneeUserId is null && x.Progress.AssigneeDepartmentId == share.DepartmentId).ToArray();
            var identity = rows.FirstOrDefault();
            var current = rows
                .Where(x => x.Progress.Status is TaskState.InProgress or TaskState.Overdue)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();
            views.Add(new ProjectParticipantView(
                share.UserId,
                share.UserId is null ? "" : identity?.AssigneeName ?? "",
                share.UserId is null ? null : identity?.AssigneeEmployeeCode,
                share.DepartmentId ?? identity?.Progress.AssigneeDepartmentId,
                identity?.AssigneeDepartmentName,
                share.AssignedWorkItems,
                share.CompletedWorkItems,
                share.InProgressWorkItems,
                share.OverdueWorkItems,
                share.ResponsibilityPercent,
                share.ContributionPercent,
                current?.Title,
                IsSelf: share.UserId == selfUserId));
        }
        return views;
    }
}
