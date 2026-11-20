using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Dashboard;

namespace BizFlow.Application.Dashboard;

/// <summary>
/// Project dashboard reads. A "project" is an existing parent task that owns work items; this
/// query adds no storage and widens no authority. Visibility is the caller's live task-read
/// scope, so a request for someone else's project is answered with 404, not with data.
/// </summary>
public sealed class ProjectDashboardQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IProjectDashboardReader reader,
    TimeProvider clock)
{
    private const string ListOperation = "dashboard.projects.read";
    private const string DetailOperation = "dashboard.projects.detail.read";

    public async Task<ProjectDashboardPage> ListAsync(ProjectDashboardFilter filter, CancellationToken cancellationToken)
    {
        var scope = await DashboardAccess.RequireTaskReadScopeAsync(context, snapshots, audit, ListOperation, cancellationToken);
        var normalized = Normalize(filter);
        var now = clock.GetUtcNow();
        var thresholds = ProjectSizeThresholds.Default;

        var slice = await reader.ListAsync(scope, normalized, thresholds, now, cancellationToken);
        var items = slice.Items
            .Select(project => DashboardProjection.Summary(project.Header, project.WorkItems, thresholds))
            .ToArray();
        return new ProjectDashboardPage(items, normalized.Page, normalized.PageSize, slice.Total, now);
    }

    public async Task<ProjectDashboardDetailView> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var scope = await DashboardAccess.RequireTaskReadScopeAsync(context, snapshots, audit, DetailOperation, cancellationToken);
        if (projectId == Guid.Empty) throw ApplicationFault.NotFound();

        var now = clock.GetUtcNow();
        var project = await reader.GetAsync(scope, projectId, now, cancellationToken)
            ?? throw ApplicationFault.NotFound();

        var thresholds = ProjectSizeThresholds.Default;
        var allProgress = project.WorkItems.Select(x => x.Progress).ToArray();

        // The project picture is computed over every work item so each viewer sees the same
        // truthful totals; only row-level disclosure narrows to the caller's own scope.
        var summary = DashboardProjection.Summary(project.Header, allProgress, thresholds);
        var shares = ProjectDashboardPolicy.Shares(allProgress);
        var allParticipants = DashboardProjection.Participants(shares, project.WorkItems, scope.UserId);

        var canSeeAllParticipants = scope.Tenant || scope.Managed;
        var visibleWorkItems = project.WorkItems.Where(x => x.VisibleToCaller).ToArray();
        var self = allParticipants.FirstOrDefault(x => x.IsSelf);

        IReadOnlyList<ProjectParticipantView> participants;
        if (scope.Tenant)
        {
            // Company-wide task read already covers every person in the tenant.
            participants = allParticipants;
        }
        else if (scope.Managed)
        {
            // A manager reads the people behind the work they can actually see, plus themselves.
            // Percentages stay shares of the whole project, so the numbers remain comparable.
            participants = allParticipants.Where(participant => participant.IsSelf
                || HoldsVisibleWork(participant, visibleWorkItems)).ToArray();
        }
        else
        {
            // Own/assigned read learns this caller's own share and nothing about who holds the rest.
            participants = self is null ? [] : new[] { self };
        }

        return new ProjectDashboardDetailView(
            Project: summary,
            WorkItems: visibleWorkItems.Select(DashboardProjection.WorkItem).ToArray(),
            Participants: participants,
            CanSeeAllParticipants: canSeeAllParticipants,
            HiddenWorkItemCount: project.WorkItems.Count - visibleWorkItems.Length,
            HiddenParticipantCount: allParticipants.Count - participants.Count,
            MyResponsibilityPercent: self?.ResponsibilityPercent ?? 0m,
            MyContributionPercent: self?.ContributionPercent ?? 0m,
            GeneratedAt: now);
    }

    private static bool HoldsVisibleWork(ProjectParticipantView participant, IReadOnlyList<ProjectWorkItemData> visible) =>
        visible.Any(item => participant.UserId is { } user
            ? item.Progress.AssigneeUserId == user
            : item.Progress.AssigneeUserId is null && item.Progress.AssigneeDepartmentId == participant.DepartmentId);

    private static ProjectDashboardFilter Normalize(ProjectDashboardFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalized = filter with
        {
            Search = DashboardAccess.Clean(filter.Search),
            Size = DashboardAccess.Clean(filter.Size)?.ToUpperInvariant()
        };
        DashboardAccess.RequirePage(normalized.Page, normalized.PageSize);
        if (normalized.Search?.Length > 200 ||
            normalized.Size is not (null or "LARGE" or "SMALL") ||
            normalized.DepartmentId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED",
                "Use a size of LARGE or SMALL, a search of at most 200 characters and a real department.");
        return normalized;
    }
}
