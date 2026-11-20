using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Dashboard;

/// <summary>
/// The employee dashboard. The subject is always the authenticated caller: no user identifier is
/// accepted from the request, so calling this endpoint with someone else's id is not expressible.
/// </summary>
public sealed class MyWorkDashboardQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IMyWorkDashboardReader reader,
    TimeProvider clock)
{
    private const string Operation = "dashboard.my-work.read";

    public async Task<MyWorkDashboardView> GetAsync(MyWorkFilter filter, CancellationToken cancellationToken)
    {
        var scope = await DashboardAccess.RequireTaskReadScopeAsync(context, snapshots, audit, Operation, cancellationToken);
        var normalized = Normalize(filter);
        var now = clock.GetUtcNow();
        var thresholds = ProjectSizeThresholds.Default;

        var slice = await reader.GetAsync(scope, normalized, now, cancellationToken);
        var progress = slice.Items.Select(x => x.Item.Progress).ToArray();

        return new MyWorkDashboardView(
            Items: slice.Items
                .Select(x => new MyWorkItemView(DashboardProjection.WorkItem(x.Item), x.ProjectId, x.ProjectTitle))
                .ToArray(),
            Page: normalized.Page,
            PageSize: normalized.PageSize,
            Total: slice.Total,
            MyProgressPercent: ProjectDashboardPolicy.WeightedMean(progress),
            ActiveWorkItems: progress.Count(x => ProjectDashboardPolicy.IsActive(x.Status)),
            CompletedWorkItems: progress.Count(x => ProjectDashboardPolicy.IsCompleted(x.Status)),
            OverdueWorkItems: progress.Count(x => x.IsOverdue),
            PendingDepartmentClaims: slice.PendingDepartmentClaims,
            ByStatus: progress
                .GroupBy(x => TaskListCodes.State(x.Status))
                .ToDictionary(group => group.Key, group => group.Count()),
            Projects: slice.Projects
                .Select(project => DashboardProjection.Summary(project.Header, project.WorkItems, thresholds))
                .ToArray(),
            GeneratedAt: now);
    }

    private static MyWorkFilter Normalize(MyWorkFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalized = filter with { Status = DashboardAccess.Clean(filter.Status) };
        DashboardAccess.RequirePage(normalized.Page, normalized.PageSize);
        if (normalized.Status is not null &&
            !Enum.GetValues<TaskState>().Any(state => TaskListCodes.State(state) == normalized.Status))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid task status.");
        return normalized;
    }
}
