using BizFlow.Domain.Dashboard;

namespace BizFlow.Application.Dashboard;

/// <summary>
/// Narrows the manager dashboard to one department. Omitting it uses the caller's whole
/// configured management scope, which <see cref="Domain.Organization.ManagementScopeExpansion"/>
/// has already expanded across descendant departments.
/// </summary>
public sealed record TeamDashboardFilter(Guid? DepartmentId = null);

public sealed record TeamMemberData(
    Guid UserId,
    string FullName,
    string EmployeeCode,
    Guid? DepartmentId,
    string? DepartmentName,
    string Status,
    IReadOnlyList<WorkItemProgressInput> WorkItems);

public sealed record TeamDepartmentData(
    Guid DepartmentId,
    string Name,
    Guid? ParentDepartmentId,
    int MemberCount);

public sealed record TeamSlice(
    IReadOnlyList<TeamDepartmentData> Departments,
    IReadOnlyList<TeamMemberData> Members,
    bool Truncated);

public interface ITeamDashboardReader
{
    /// <summary>
    /// Reads members of the given departments and the work they hold. A null
    /// <paramref name="departmentIds"/> means the caller holds tenant-wide task read and the whole
    /// tenant is in scope; an empty set means nothing is in scope and must return no members.
    /// The work counted is additionally intersected with <paramref name="scope"/>, so a team view
    /// never totals a task the caller could not open.
    /// </summary>
    Task<TeamSlice> GetAsync(Domain.Tasks.TaskReadScope scope, IReadOnlySet<Guid>? departmentIds,
        int memberLimit, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed record TeamMemberView(
    Guid UserId,
    string FullName,
    string EmployeeCode,
    Guid? DepartmentId,
    string? DepartmentName,
    string Status,
    int TotalAssigned,
    int ActiveWorkItems,
    int CompletedWorkItems,
    int OverdueWorkItems,
    int AvgProgressPercent,
    decimal ResponsibilitySharePercent);

public sealed record TeamDepartmentView(
    Guid DepartmentId,
    string Name,
    Guid? ParentDepartmentId,
    int MemberCount,
    int ActiveWorkItems,
    int OverdueWorkItems,
    int AvgProgressPercent);

public sealed record TeamDashboardView(
    IReadOnlyList<TeamDepartmentView> Departments,
    IReadOnlyList<TeamMemberView> Members,
    IReadOnlyList<Guid> ScopeDepartmentIds,
    bool TenantWide,
    bool Truncated,
    int MemberLimit,
    DateTimeOffset GeneratedAt);
