using BizFlow.Domain.Tasks;

namespace BizFlow.Domain.Dashboard;

public enum ProjectSizeCategory { Small, Large }

/// <summary>
/// One server-resolved work item (subtask) of a project. Deleted rows are excluded by the
/// caller; <see cref="IsOverdue"/> is resolved against the server clock, never a client value.
/// </summary>
public sealed record WorkItemProgressInput(
    Guid TaskId,
    TaskState Status,
    TaskPriority Priority,
    int ChecklistTotal,
    int ChecklistDone,
    Guid? AssigneeUserId,
    Guid? AssigneeDepartmentId,
    bool IsOverdue);

public sealed record ProjectProgress(
    int ProgressPercent,
    int WorkItemCount,
    int CompletedWorkItemCount,
    int CancelledWorkItemCount,
    int OverdueWorkItemCount,
    int UnassignedWorkItemCount,
    int ParticipantCount,
    ProjectSizeCategory SizeCategory);

public sealed record ParticipantShare(
    Guid? UserId,
    Guid? DepartmentId,
    int AssignedWorkItems,
    int CompletedWorkItems,
    int InProgressWorkItems,
    int OverdueWorkItems,
    decimal ResponsibilityPercent,
    decimal ContributionPercent);

/// <summary>
/// Pure projection rules for the project dashboard. This type performs no I/O, holds no state
/// and grants no authority: visibility is resolved by <see cref="TaskReadPolicy"/> before any
/// row reaches these functions. A project is an existing parent <see cref="WorkTask"/>
/// (ParentTaskId IS NULL) that owns at least one subtask; no new storage is introduced.
/// </summary>
public static class ProjectDashboardPolicy
{
    // Owner-approved classification thresholds. A tenant-configurable override is intentionally
    // not read here: TenantSetting validates against a closed key whitelist, and widening it is
    // a separate, explicitly approved change.
    public const int DefaultLargeProjectMinWorkItems = 10;
    public const int DefaultLargeProjectMinParticipants = 5;

    // Priority is persisted as a checked string enum, so an undefined value is corrupt data
    // and must fail loudly rather than silently distort a reported percentage.
    public static int Weight(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => 1,
        TaskPriority.Medium => 2,
        TaskPriority.High => 3,
        TaskPriority.Critical => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown task priority.")
    };

    /// <summary>
    /// Canonical state-to-progress mapping. Overdue is reported at the InProgress value because
    /// §9.1 only reaches Overdue from InProgress or Submitted and the row does not retain which:
    /// the lower value is used so progress is never overstated. Cancelled work is excluded from
    /// every aggregate and its value is therefore never used as a numerator.
    /// </summary>
    public static int BasePercent(TaskState status) => status switch
    {
        TaskState.Draft => 0,
        TaskState.Rejected => 0,
        TaskState.Cancelled => 0,
        TaskState.Assigned => 5,
        TaskState.Accepted => 10,
        TaskState.InProgress => 50,
        TaskState.Overdue => 50,
        TaskState.Submitted => 80,
        TaskState.Confirmed => 95,
        TaskState.Completed => 100,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task state.")
    };

    /// <summary>
    /// Progress of a single work item. A checklist refines InProgress only, and stays strictly
    /// below the Submitted value so a fully ticked checklist never outranks submitted evidence.
    /// </summary>
    public static int WorkItemPercent(TaskState status, int checklistTotal, int checklistDone)
    {
        if (checklistTotal < 0)
            throw new ArgumentOutOfRangeException(nameof(checklistTotal), checklistTotal, "Checklist size cannot be negative.");
        if (checklistDone < 0 || checklistDone > checklistTotal)
            throw new ArgumentOutOfRangeException(nameof(checklistDone), checklistDone, "Completed checklist items must fall within the checklist.");
        if (status == TaskState.InProgress && checklistTotal > 0)
            return 10 + (int)Math.Round(60m * checklistDone / checklistTotal, MidpointRounding.AwayFromZero);
        return BasePercent(status);
    }

    public static int WorkItemPercent(WorkItemProgressInput item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return WorkItemPercent(item.Status, item.ChecklistTotal, item.ChecklistDone);
    }

    public static bool IsCompleted(TaskState status) => status is TaskState.Completed or TaskState.Confirmed;

    // Same "active" set the existing manager report uses, so both screens agree on the word.
    public static bool IsActive(TaskState status) => status is TaskState.Assigned or TaskState.Accepted
        or TaskState.InProgress or TaskState.Submitted or TaskState.Confirmed or TaskState.Overdue;

    /// <summary>
    /// Priority-weighted mean progress of the live work items, or 0 when none are live.
    /// The minimum weight is 1, so the denominator is positive whenever it is evaluated.
    /// </summary>
    public static int WeightedMean(IReadOnlyCollection<WorkItemProgressInput> workItems)
    {
        ArgumentNullException.ThrowIfNull(workItems);
        var live = workItems.Where(x => x.Status != TaskState.Cancelled).ToArray();
        if (live.Length == 0) return 0;
        var totalWeight = live.Sum(x => Weight(x.Priority));
        var earned = live.Sum(x => Weight(x.Priority) * (long)WorkItemPercent(x));
        return (int)Math.Round((decimal)earned / totalWeight, MidpointRounding.AwayFromZero);
    }

    public static ProjectSizeCategory Classify(int workItemCount, int participantCount,
        int minWorkItems = DefaultLargeProjectMinWorkItems, int minParticipants = DefaultLargeProjectMinParticipants)
    {
        if (minWorkItems < 1 || minParticipants < 1)
            throw new ArgumentOutOfRangeException(nameof(minWorkItems), "Classification thresholds must be positive.");
        return workItemCount >= minWorkItems || participantCount >= minParticipants
            ? ProjectSizeCategory.Large
            : ProjectSizeCategory.Small;
    }

    /// <summary>
    /// Weighted project aggregate. A project with no live work item falls back to the parent
    /// task's own state so a legacy parent task never reports a misleading 0%, and the weighted
    /// mean is only evaluated when the denominator is provably positive (minimum weight is 1).
    /// </summary>
    public static ProjectProgress Aggregate(TaskState projectStatus, IReadOnlyCollection<WorkItemProgressInput> workItems,
        int minWorkItems = DefaultLargeProjectMinWorkItems, int minParticipants = DefaultLargeProjectMinParticipants)
    {
        ArgumentNullException.ThrowIfNull(workItems);
        var live = workItems.Where(x => x.Status != TaskState.Cancelled).ToArray();
        var cancelled = workItems.Count - live.Length;
        var participants = live.Where(x => x.AssigneeUserId is not null)
            .Select(x => x.AssigneeUserId!.Value).Distinct().Count();

        var progress = live.Length == 0 ? BasePercent(projectStatus) : WeightedMean(live);

        return new ProjectProgress(
            ProgressPercent: progress,
            WorkItemCount: live.Length,
            CompletedWorkItemCount: live.Count(x => IsCompleted(x.Status)),
            CancelledWorkItemCount: cancelled,
            OverdueWorkItemCount: live.Count(x => x.IsOverdue),
            UnassignedWorkItemCount: live.Count(x => x.AssigneeUserId is null && x.AssigneeDepartmentId is null),
            ParticipantCount: participants,
            SizeCategory: Classify(live.Length, participants, minWorkItems, minParticipants));
    }

    /// <summary>
    /// Responsibility and contribution split. Responsibility is the weighted share of the work
    /// a participant currently holds; contribution is their weighted share of progress actually
    /// earned. Work assigned to a department but not yet claimed is reported as its own row so
    /// no workload silently disappears from the split. Shares are percentages of the assigned
    /// weight only: unassigned work items are excluded here but still count toward project
    /// progress, so a caller must surface UnassignedWorkItemCount alongside this split.
    /// </summary>
    public static IReadOnlyList<ParticipantShare> Shares(IReadOnlyCollection<WorkItemProgressInput> workItems)
    {
        ArgumentNullException.ThrowIfNull(workItems);
        var assigned = workItems
            .Where(x => x.Status != TaskState.Cancelled && (x.AssigneeUserId is not null || x.AssigneeDepartmentId is not null))
            .ToArray();
        if (assigned.Length == 0) return [];

        var totalWeight = assigned.Sum(x => Weight(x.Priority));
        var totalEarned = assigned.Sum(x => Weight(x.Priority) * (long)WorkItemPercent(x));

        return assigned
            // A claimed item groups by its user regardless of which department routed it;
            // an unclaimed item groups by the department that still owes the work.
            .GroupBy(x => (UserId: x.AssigneeUserId,
                DepartmentId: x.AssigneeUserId is null ? x.AssigneeDepartmentId : null))
            .Select(group =>
            {
                var weight = group.Sum(x => Weight(x.Priority));
                var earned = group.Sum(x => Weight(x.Priority) * (long)WorkItemPercent(x));
                return new ParticipantShare(
                    UserId: group.Key.UserId,
                    DepartmentId: group.Key.DepartmentId,
                    AssignedWorkItems: group.Count(),
                    CompletedWorkItems: group.Count(x => IsCompleted(x.Status)),
                    InProgressWorkItems: group.Count(x => x.Status is TaskState.InProgress or TaskState.Overdue),
                    OverdueWorkItems: group.Count(x => x.IsOverdue),
                    ResponsibilityPercent: Percent(weight, totalWeight),
                    ContributionPercent: Percent(earned, totalEarned));
            })
            .OrderByDescending(x => x.ResponsibilityPercent)
            .ThenByDescending(x => x.AssignedWorkItems)
            .ToArray();
    }

    // A zero denominator means "nothing earned yet", not an error: report 0 and never divide.
    private static decimal Percent(long part, long total) =>
        total <= 0 ? 0m : Math.Round(part * 100m / total, 1, MidpointRounding.AwayFromZero);
}
