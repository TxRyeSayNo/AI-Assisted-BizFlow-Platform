using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Dashboard;

public sealed class ProjectDashboardPolicyTests
{
    [Theory]
    [InlineData(TaskPriority.Low, 1)]
    [InlineData(TaskPriority.Medium, 2)]
    [InlineData(TaskPriority.High, 3)]
    [InlineData(TaskPriority.Critical, 5)]
    public void Weight_follows_the_approved_priority_scale(TaskPriority priority, int expected) =>
        Assert.Equal(expected, ProjectDashboardPolicy.Weight(priority));

    [Fact]
    public void Every_canonical_state_has_a_defined_percent_and_unknown_values_fail_loudly()
    {
        foreach (var state in Enum.GetValues<TaskState>())
        {
            var percent = ProjectDashboardPolicy.BasePercent(state);
            Assert.InRange(percent, 0, 100);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.BasePercent((TaskState)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.Weight((TaskPriority)999));
    }

    [Theory]
    [InlineData(TaskState.Draft, 0)]
    [InlineData(TaskState.Assigned, 5)]
    [InlineData(TaskState.Accepted, 10)]
    [InlineData(TaskState.InProgress, 50)]
    [InlineData(TaskState.Overdue, 50)]
    [InlineData(TaskState.Submitted, 80)]
    [InlineData(TaskState.Confirmed, 95)]
    [InlineData(TaskState.Completed, 100)]
    [InlineData(TaskState.Rejected, 0)]
    public void A_work_item_without_a_checklist_reports_its_state_percent(TaskState state, int expected) =>
        Assert.Equal(expected, ProjectDashboardPolicy.WorkItemPercent(state, 0, 0));

    [Theory]
    [InlineData(4, 0, 10)]
    [InlineData(4, 1, 25)]
    [InlineData(4, 2, 40)]
    [InlineData(4, 3, 55)]
    [InlineData(4, 4, 70)]
    [InlineData(3, 1, 30)]
    [InlineData(1, 1, 70)]
    public void A_checklist_refines_in_progress_work_without_outranking_submitted_evidence(int total, int done, int expected)
    {
        Assert.Equal(expected, ProjectDashboardPolicy.WorkItemPercent(TaskState.InProgress, total, done));
        Assert.True(expected < ProjectDashboardPolicy.BasePercent(TaskState.Submitted));
        Assert.True(expected >= ProjectDashboardPolicy.BasePercent(TaskState.Accepted));
    }

    [Fact]
    public void A_checklist_never_overrides_a_state_other_than_in_progress()
    {
        foreach (var state in Enum.GetValues<TaskState>().Where(s => s != TaskState.InProgress))
            Assert.Equal(ProjectDashboardPolicy.BasePercent(state), ProjectDashboardPolicy.WorkItemPercent(state, 4, 4));
    }

    [Fact]
    public void A_malformed_checklist_count_is_rejected_instead_of_producing_a_percentage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.WorkItemPercent(TaskState.InProgress, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.WorkItemPercent(TaskState.InProgress, 2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.WorkItemPercent(TaskState.InProgress, 2, 3));
    }

    [Fact]
    public void A_project_without_work_items_falls_back_to_its_own_state_and_never_divides_by_zero()
    {
        var empty = ProjectDashboardPolicy.Aggregate(TaskState.InProgress, []);
        Assert.Equal(50, empty.ProgressPercent);
        Assert.Equal(0, empty.WorkItemCount);
        Assert.Equal(0, empty.ParticipantCount);
        Assert.Equal(ProjectSizeCategory.Small, empty.SizeCategory);

        var draft = ProjectDashboardPolicy.Aggregate(TaskState.Draft, []);
        Assert.Equal(0, draft.ProgressPercent);
    }

    [Fact]
    public void Cancelled_work_leaves_the_denominator_and_an_all_cancelled_project_falls_back()
    {
        var items = new[] { Item(TaskState.Cancelled, TaskPriority.Critical), Item(TaskState.Cancelled, TaskPriority.Low) };
        var result = ProjectDashboardPolicy.Aggregate(TaskState.Completed, items);
        Assert.Equal(100, result.ProgressPercent);
        Assert.Equal(0, result.WorkItemCount);
        Assert.Equal(2, result.CancelledWorkItemCount);

        // A cancelled Critical item must not drag a finished Low item below 100%.
        var mixed = ProjectDashboardPolicy.Aggregate(TaskState.InProgress,
            [Item(TaskState.Completed, TaskPriority.Low), Item(TaskState.Cancelled, TaskPriority.Critical)]);
        Assert.Equal(100, mixed.ProgressPercent);
        Assert.Equal(1, mixed.WorkItemCount);
    }

    [Fact]
    public void Project_progress_is_the_priority_weighted_mean_of_live_work_items()
    {
        // Critical(5) at 100 and Low(1) at 0 => 500 / 6 = 83.33 -> 83
        var result = ProjectDashboardPolicy.Aggregate(TaskState.InProgress,
            [Item(TaskState.Completed, TaskPriority.Critical), Item(TaskState.Draft, TaskPriority.Low)]);
        Assert.Equal(83, result.ProgressPercent);
        Assert.Equal(2, result.WorkItemCount);
        Assert.Equal(1, result.CompletedWorkItemCount);
    }

    [Fact]
    public void Confirmed_counts_as_completed_exactly_as_the_existing_task_screen_does()
    {
        var result = ProjectDashboardPolicy.Aggregate(TaskState.InProgress,
            [Item(TaskState.Confirmed, TaskPriority.Medium), Item(TaskState.Completed, TaskPriority.Medium)]);
        Assert.Equal(2, result.CompletedWorkItemCount);
        Assert.True(ProjectDashboardPolicy.IsCompleted(TaskState.Confirmed));
        Assert.True(ProjectDashboardPolicy.IsCompleted(TaskState.Completed));
        Assert.False(ProjectDashboardPolicy.IsCompleted(TaskState.Submitted));
    }

    [Fact]
    public void Counts_separate_overdue_unassigned_and_distinct_participants()
    {
        var alice = Guid.NewGuid();
        var department = Guid.NewGuid();
        var items = new[]
        {
            Item(TaskState.InProgress, TaskPriority.Medium, user: alice, overdue: true),
            Item(TaskState.Submitted, TaskPriority.Medium, user: alice),
            Item(TaskState.Assigned, TaskPriority.Medium, department: department),
            Item(TaskState.Draft, TaskPriority.Medium)
        };
        var result = ProjectDashboardPolicy.Aggregate(TaskState.InProgress, items);
        Assert.Equal(1, result.ParticipantCount);
        Assert.Equal(1, result.OverdueWorkItemCount);
        Assert.Equal(1, result.UnassignedWorkItemCount);
        Assert.Equal(4, result.WorkItemCount);
    }

    [Theory]
    [InlineData(9, 4, ProjectSizeCategory.Small)]
    [InlineData(10, 0, ProjectSizeCategory.Large)]
    [InlineData(0, 5, ProjectSizeCategory.Large)]
    [InlineData(10, 5, ProjectSizeCategory.Large)]
    [InlineData(0, 0, ProjectSizeCategory.Small)]
    public void Classification_is_large_when_either_default_threshold_is_reached(int workItems, int participants, ProjectSizeCategory expected) =>
        Assert.Equal(expected, ProjectDashboardPolicy.Classify(workItems, participants));

    [Fact]
    public void Classification_thresholds_are_overridable_and_must_stay_positive()
    {
        Assert.Equal(ProjectSizeCategory.Large, ProjectDashboardPolicy.Classify(3, 1, minWorkItems: 3, minParticipants: 99));
        Assert.Equal(ProjectSizeCategory.Small, ProjectDashboardPolicy.Classify(3, 1, minWorkItems: 4, minParticipants: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.Classify(1, 1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectDashboardPolicy.Classify(1, 1, 1, 0));
    }

    [Fact]
    public void Responsibility_sums_to_one_hundred_percent_across_assigned_participants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var shares = ProjectDashboardPolicy.Shares(
        [
            Item(TaskState.Completed, TaskPriority.Critical, user: alice),   // weight 5
            Item(TaskState.Draft, TaskPriority.Low, user: alice),            // weight 1
            Item(TaskState.Draft, TaskPriority.Medium, user: bob)            // weight 2
        ]);
        Assert.Equal(2, shares.Count);
        Assert.Equal(100m, shares.Sum(x => x.ResponsibilityPercent));
        var aliceShare = Assert.Single(shares, x => x.UserId == alice);
        Assert.Equal(75m, aliceShare.ResponsibilityPercent);
        Assert.Equal(2, aliceShare.AssignedWorkItems);
        Assert.Equal(1, aliceShare.CompletedWorkItems);
        // Alice earned 5*100, Bob earned 2*0 => Alice holds all contribution.
        Assert.Equal(100m, aliceShare.ContributionPercent);
        Assert.Equal(0m, Assert.Single(shares, x => x.UserId == bob).ContributionPercent);
    }

    [Fact]
    public void Work_claimed_by_nobody_yet_is_reported_as_its_own_department_row()
    {
        var department = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var shares = ProjectDashboardPolicy.Shares(
        [
            Item(TaskState.Draft, TaskPriority.Medium, user: alice),
            Item(TaskState.Draft, TaskPriority.Medium, department: department)
        ]);
        Assert.Equal(2, shares.Count);
        var pending = Assert.Single(shares, x => x.UserId is null);
        Assert.Equal(department, pending.DepartmentId);
        Assert.Equal(50m, pending.ResponsibilityPercent);
        Assert.Equal(100m, shares.Sum(x => x.ResponsibilityPercent));
    }

    [Fact]
    public void Nothing_assigned_or_nothing_earned_yields_an_empty_or_zero_split_without_error()
    {
        Assert.Empty(ProjectDashboardPolicy.Shares([]));
        Assert.Empty(ProjectDashboardPolicy.Shares([Item(TaskState.Draft, TaskPriority.Medium)]));
        Assert.Empty(ProjectDashboardPolicy.Shares([Item(TaskState.Cancelled, TaskPriority.Medium, user: Guid.NewGuid())]));

        var share = Assert.Single(ProjectDashboardPolicy.Shares([Item(TaskState.Draft, TaskPriority.Medium, user: Guid.NewGuid())]));
        Assert.Equal(100m, share.ResponsibilityPercent);
        Assert.Equal(0m, share.ContributionPercent);
    }

    [Fact]
    public void Legacy_rows_without_assignment_checklist_or_subtasks_never_throw()
    {
        var legacy = new[] { Item(TaskState.Draft, TaskPriority.Medium), Item(TaskState.Assigned, TaskPriority.Low) };
        var aggregate = ProjectDashboardPolicy.Aggregate(TaskState.Draft, legacy);
        Assert.InRange(aggregate.ProgressPercent, 0, 100);
        Assert.Equal(2, aggregate.UnassignedWorkItemCount);
        Assert.Empty(ProjectDashboardPolicy.Shares(legacy));
    }

    private static WorkItemProgressInput Item(TaskState status, TaskPriority priority,
        Guid? user = null, Guid? department = null, bool overdue = false, int checklistTotal = 0, int checklistDone = 0) =>
        new(Guid.NewGuid(), status, priority, checklistTotal, checklistDone, user, department, overdue);
}
