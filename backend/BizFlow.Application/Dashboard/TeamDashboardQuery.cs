using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Dashboard;

/// <summary>
/// The subordinate-management dashboard. It requires managed or tenant-wide task read, and a
/// department filter must fall inside the caller's own configured management scope: one manager
/// can never read another manager's team by passing that team's department id.
/// </summary>
public sealed class TeamDashboardQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    ITeamDashboardReader reader,
    TimeProvider clock)
{
    private const string Operation = "dashboard.team.read";

    // A management scope is an organizational unit, not a report range; this cap keeps one
    // request bounded while staying far above a realistic department size.
    public const int MemberLimit = 200;

    public async Task<TeamDashboardView> GetAsync(TeamDashboardFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var scope = await DashboardAccess.RequireTaskReadScopeAsync(context, snapshots, audit, Operation, cancellationToken);

        // Own/assigned task read is a personal capability and never confers sight of other people.
        if (!scope.Tenant && !scope.Managed)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, Operation,
                Domain.Security.AccessDenial.PermissionDenied, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED",
                "Only a manager of this team can read its dashboard.");
        }

        IReadOnlySet<Guid>? departmentIds;
        if (filter.DepartmentId is { } requested)
        {
            if (requested == Guid.Empty)
                throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a real department.");
            if (!scope.Tenant && !scope.ManagedDepartmentIds.Contains(requested))
            {
                await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, Operation,
                    Domain.Security.AccessDenial.ManagementScopeDenied, cancellationToken);
                throw new ApplicationFault(FaultKind.Forbidden, "TASK.TARGET_OUT_OF_SCOPE",
                    "The requested department is outside your configured management scope.");
            }
            departmentIds = new HashSet<Guid> { requested };
        }
        else
        {
            // Tenant-wide read sees every department; a manager sees exactly their own scope,
            // which may legitimately be empty and must then return no members rather than fail.
            departmentIds = scope.Tenant ? null : scope.ManagedDepartmentIds;
        }

        var now = clock.GetUtcNow();
        var slice = await reader.GetAsync(scope, departmentIds, MemberLimit, now, cancellationToken);

        // A company-wide reader is not scope-checked above, so a department that does not exist in
        // this tenant must fail closed here rather than return a convincing empty team.
        if (filter.DepartmentId is not null && slice.Departments.Count == 0) throw ApplicationFault.NotFound();

        var totalWeight = slice.Members.Sum(member => LiveWeight(member.WorkItems));
        var members = slice.Members
            .Select(member => new TeamMemberView(
                member.UserId,
                member.FullName,
                member.EmployeeCode,
                member.DepartmentId,
                member.DepartmentName,
                member.Status,
                TotalAssigned: member.WorkItems.Count,
                ActiveWorkItems: member.WorkItems.Count(x => ProjectDashboardPolicy.IsActive(x.Status)),
                CompletedWorkItems: member.WorkItems.Count(x => ProjectDashboardPolicy.IsCompleted(x.Status)),
                OverdueWorkItems: member.WorkItems.Count(x => x.IsOverdue),
                AvgProgressPercent: ProjectDashboardPolicy.WeightedMean(member.WorkItems),
                ResponsibilitySharePercent: Share(LiveWeight(member.WorkItems), totalWeight)))
            .OrderByDescending(x => x.ActiveWorkItems)
            .ThenBy(x => x.FullName, StringComparer.CurrentCulture)
            .ToArray();

        var byDepartment = slice.Members
            .Where(member => member.DepartmentId is not null)
            .GroupBy(member => member.DepartmentId!.Value)
            .ToDictionary(group => group.Key, group => group.SelectMany(member => member.WorkItems).ToArray());

        var departments = slice.Departments
            .Select(department =>
            {
                var items = byDepartment.TryGetValue(department.DepartmentId, out var rows)
                    ? rows
                    : Array.Empty<WorkItemProgressInput>();
                return new TeamDepartmentView(
                    department.DepartmentId,
                    department.Name,
                    department.ParentDepartmentId,
                    department.MemberCount,
                    ActiveWorkItems: items.Count(x => ProjectDashboardPolicy.IsActive(x.Status)),
                    OverdueWorkItems: items.Count(x => x.IsOverdue),
                    AvgProgressPercent: ProjectDashboardPolicy.WeightedMean(items));
            })
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ToArray();

        return new TeamDashboardView(
            Departments: departments,
            Members: members,
            ScopeDepartmentIds: departmentIds?.ToArray() ?? [],
            TenantWide: departmentIds is null,
            Truncated: slice.Truncated,
            MemberLimit: MemberLimit,
            GeneratedAt: now);
    }

    private static long LiveWeight(IReadOnlyList<WorkItemProgressInput> items) =>
        items.Where(x => x.Status != TaskState.Cancelled).Sum(x => (long)ProjectDashboardPolicy.Weight(x.Priority));

    private static decimal Share(long part, long total) =>
        total <= 0 ? 0m : Math.Round(part * 100m / total, 1, MidpointRounding.AwayFromZero);
}
