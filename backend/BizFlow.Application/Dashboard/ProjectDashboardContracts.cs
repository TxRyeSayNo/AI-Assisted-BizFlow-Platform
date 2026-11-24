using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Dashboard;

// ---------------------------------------------------------------------------------------------
// Request shapes
// ---------------------------------------------------------------------------------------------

public sealed record ProjectDashboardFilter(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Size = null,
    Guid? DepartmentId = null);

public sealed record MyWorkFilter(int Page = 1, int PageSize = 25, string? Status = null);

/// <summary>Classification thresholds resolved by the Application layer, never by a caller.</summary>
public sealed record ProjectSizeThresholds(int MinWorkItems, int MinParticipants)
{
    public static ProjectSizeThresholds Default { get; } = new(
        ProjectDashboardPolicy.DefaultLargeProjectMinWorkItems,
        ProjectDashboardPolicy.DefaultLargeProjectMinParticipants);
}

// ---------------------------------------------------------------------------------------------
// Storage-facing shapes. Infrastructure resolves rows; ProjectDashboardPolicy owns the maths.
// ---------------------------------------------------------------------------------------------

public sealed record ProjectHeaderData(
    Guid ProjectId,
    string Title,
    TaskState Status,
    TaskPriority Priority,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? OwnerUserId,
    string? OwnerName,
    Guid? OwnerDepartmentId,
    string? OwnerDepartmentName);

/// <summary>
/// A work item with its display fields. <see cref="VisibleToCaller"/> records whether the caller's
/// own task-read scope reaches this row: project-level aggregates are computed over every row so
/// the shared project picture stays truthful, while row-level disclosure honours the scope.
/// </summary>
public sealed record ProjectWorkItemData(
    WorkItemProgressInput Progress,
    string Title,
    DateTimeOffset? Deadline,
    DateTimeOffset UpdatedAt,
    string? AssigneeName,
    string? AssigneeEmployeeCode,
    string? AssigneeDepartmentName,
    bool VisibleToCaller);

public sealed record ProjectSummaryData(ProjectHeaderData Header, IReadOnlyList<WorkItemProgressInput> WorkItems);

public sealed record ProjectData(ProjectHeaderData Header, IReadOnlyList<ProjectWorkItemData> WorkItems);

public sealed record ProjectListSlice(IReadOnlyList<ProjectSummaryData> Items, long Total);

public sealed record MyWorkItemData(ProjectWorkItemData Item, Guid? ProjectId, string? ProjectTitle);

public sealed record MyWorkSlice(
    IReadOnlyList<MyWorkItemData> Items,
    long Total,
    IReadOnlyList<ProjectSummaryData> Projects,
    int PendingDepartmentClaims);

public interface IProjectDashboardReader
{
    Task<ProjectListSlice> ListAsync(TaskReadScope scope, ProjectDashboardFilter filter,
        ProjectSizeThresholds thresholds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ProjectData?> GetAsync(TaskReadScope scope, Guid projectId, DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Summaries for specific projects, filtered through the same visibility rules as the list.
    /// Unreachable or non-project identifiers are simply absent from the result.
    /// </summary>
    Task<IReadOnlyList<ProjectSummaryData>> SummariesAsync(TaskReadScope scope,
        IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IMyWorkDashboardReader
{
    Task<MyWorkSlice> GetAsync(TaskReadScope scope, MyWorkFilter filter, DateTimeOffset now,
        CancellationToken cancellationToken);
}

// ---------------------------------------------------------------------------------------------
// API shapes
// ---------------------------------------------------------------------------------------------

public sealed record ProjectSummaryView(
    Guid ProjectId,
    string Title,
    string Status,
    string Priority,
    DateTimeOffset? Deadline,
    Guid? OwnerUserId,
    string? OwnerName,
    Guid? OwnerDepartmentId,
    string? OwnerDepartmentName,
    string SizeCategory,
    int ProgressPercent,
    int WorkItemCount,
    int CompletedWorkItemCount,
    int CancelledWorkItemCount,
    int OverdueWorkItemCount,
    int UnassignedWorkItemCount,
    int ParticipantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectDashboardPage(
    IReadOnlyList<ProjectSummaryView> Items,
    int Page,
    int PageSize,
    long Total,
    DateTimeOffset GeneratedAt);

public sealed record ProjectWorkItemView(
    Guid TaskId,
    string Title,
    string Status,
    string Priority,
    DateTimeOffset? Deadline,
    int Weight,
    int ProgressPercent,
    int ChecklistTotal,
    int ChecklistDone,
    Guid? AssigneeUserId,
    string? AssigneeName,
    Guid? AssigneeDepartmentId,
    string? AssigneeDepartmentName,
    bool IsOverdue,
    DateTimeOffset UpdatedAt);

public sealed record ProjectParticipantView(
    Guid? UserId,
    string FullName,
    string? EmployeeCode,
    Guid? DepartmentId,
    string? DepartmentName,
    int AssignedWorkItems,
    int CompletedWorkItems,
    int InProgressWorkItems,
    int OverdueWorkItems,
    decimal ResponsibilityPercent,
    decimal ContributionPercent,
    string? CurrentWorkItemTitle,
    bool IsSelf);

public sealed record ProjectDashboardDetailView(
    ProjectSummaryView Project,
    IReadOnlyList<ProjectWorkItemView> WorkItems,
    IReadOnlyList<ProjectParticipantView> Participants,
    bool CanSeeAllParticipants,
    int HiddenWorkItemCount,
    int HiddenParticipantCount,
    decimal MyResponsibilityPercent,
    decimal MyContributionPercent,
    DateTimeOffset GeneratedAt);

public sealed record MyWorkItemView(
    ProjectWorkItemView WorkItem,
    Guid? ProjectId,
    string? ProjectTitle);

public sealed record MyWorkDashboardView(
    IReadOnlyList<MyWorkItemView> Items,
    int Page,
    int PageSize,
    long Total,
    int MyProgressPercent,
    int ActiveWorkItems,
    int CompletedWorkItems,
    int OverdueWorkItems,
    int PendingDepartmentClaims,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyList<ProjectSummaryView> Projects,
    DateTimeOffset GeneratedAt);
