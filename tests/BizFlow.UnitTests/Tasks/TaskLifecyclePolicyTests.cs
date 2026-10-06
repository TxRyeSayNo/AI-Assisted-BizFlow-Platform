using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskLifecyclePolicyTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Department = Guid.NewGuid();
    private static readonly ResourceScope Task = new(Tenant, User, Department, [User]);
    private static readonly AccessSnapshot Authorized = new(User, Tenant, true, true,
        [new("tasks.assign", PermissionScope.Tenant), new("tasks.accept", PermissionScope.Tenant),
         new("tasks.execute", PermissionScope.Tenant), new("tasks.submit", PermissionScope.Tenant),
         new("tasks.confirm", PermissionScope.Tenant), new("tasks.cancel", PermissionScope.Tenant)],
        new HashSet<Guid> { Department }, new HashSet<Guid> { Department });
    private static readonly TaskTransitionEvidence Valid = new(true, true, true, true, true, true, true, true, "Business reason");

    [Fact]
    public void Exact_canonical_states_and_every_user_state_pair_are_checked()
    {
        Assert.Equal(10, Enum.GetValues<TaskState>().Length);
        HashSet<(TaskState, TaskState)> allowed = [
            (TaskState.Draft, TaskState.Assigned), (TaskState.Rejected, TaskState.Assigned),
            (TaskState.Assigned, TaskState.Accepted), (TaskState.Assigned, TaskState.Rejected),
            (TaskState.Accepted, TaskState.InProgress), (TaskState.Overdue, TaskState.InProgress),
            (TaskState.InProgress, TaskState.Submitted), (TaskState.Submitted, TaskState.Confirmed),
            (TaskState.Submitted, TaskState.InProgress), (TaskState.Confirmed, TaskState.Completed)
        ];
        foreach (var from in Enum.GetValues<TaskState>())
        {
            if (from is not (TaskState.Completed or TaskState.Cancelled)) allowed.Add((from, TaskState.Cancelled));
            foreach (var to in Enum.GetValues<TaskState>())
                Assert.Equal(allowed.Contains((from, to)), Evaluate(from, to).Allowed);
        }
    }

    [Fact]
    public void Only_system_overdue_detection_and_confirmed_completion_are_system_edges()
    {
        foreach (var from in Enum.GetValues<TaskState>())
            foreach (var to in Enum.GetValues<TaskState>())
            {
                var expected = (from is TaskState.InProgress or TaskState.Submitted && to == TaskState.Overdue) ||
                    (from == TaskState.Confirmed && to == TaskState.Completed);
                Assert.Equal(expected, TaskLifecyclePolicy.EvaluateSystem(from, to, Tenant, true, Valid).Allowed);
            }
        Assert.Equal(TaskTransitionDenial.SystemOnly, Evaluate(TaskState.InProgress, TaskState.Overdue).Denial);
        Assert.Equal(TaskTransitionDenial.ThresholdNotCrossed, TaskLifecyclePolicy.EvaluateSystem(
            TaskState.InProgress, TaskState.Overdue, Tenant, true, Valid with { DeadlineOrSlaExceeded = false }).Denial);
        Assert.Equal(TaskTransitionDenial.InactiveTenant, TaskLifecyclePolicy.EvaluateSystem(TaskState.InProgress, TaskState.Overdue, Tenant, false, Valid).Denial);
        Assert.Equal(TaskTransitionDenial.InvalidTenant, TaskLifecyclePolicy.EvaluateSystem(TaskState.InProgress, TaskState.Overdue, Guid.Empty, true, Valid).Denial);
    }

    [Theory]
    [InlineData(TaskState.Draft, TaskState.Assigned, "tasks.assign")]
    [InlineData(TaskState.Rejected, TaskState.Assigned, "tasks.assign")]
    [InlineData(TaskState.Assigned, TaskState.Accepted, "tasks.accept")]
    [InlineData(TaskState.Assigned, TaskState.Rejected, "tasks.accept")]
    [InlineData(TaskState.Accepted, TaskState.InProgress, "tasks.execute")]
    [InlineData(TaskState.Overdue, TaskState.InProgress, "tasks.execute")]
    [InlineData(TaskState.InProgress, TaskState.Submitted, "tasks.submit")]
    [InlineData(TaskState.Submitted, TaskState.Confirmed, "tasks.confirm")]
    [InlineData(TaskState.Submitted, TaskState.InProgress, "tasks.confirm")]
    [InlineData(TaskState.Confirmed, TaskState.Completed, "tasks.confirm")]
    [InlineData(TaskState.InProgress, TaskState.Cancelled, "tasks.cancel")]
    public void Every_user_action_needs_its_exact_permission_and_current_tenant_scope(TaskState from, TaskState to, string permission)
    {
        var permitted = Authorized with { Grants = [new(permission, PermissionScope.Tenant)] };
        Assert.True(TaskLifecyclePolicy.EvaluateUser(from, to, permitted, Task, Valid, Department).Allowed);
        foreach (var forbidden in new[]
        {
            permitted with { Grants = [] }, permitted with { Grants = [new("MANAGER", PermissionScope.Tenant)] },
            permitted with { Grants = [new("*", PermissionScope.Tenant)] }, permitted with { IsUserActive = false },
            permitted with { IsTenantActive = false }, permitted with { TenantId = Guid.NewGuid() },
            permitted with { UserId = Guid.Empty }, permitted with { Grants = [new(permission, PermissionScope.Platform)] }
        })
            Assert.Equal(TaskTransitionDenial.AccessDenied, TaskLifecyclePolicy.EvaluateUser(from, to, forbidden, Task, Valid, Department).Denial);
        Assert.Equal(TaskTransitionDenial.WorkflowDenied, Evaluate(from, to, Valid with { WorkflowAllowsTransition = false }).Denial);
    }

    [Fact]
    public void Assignment_requires_a_validated_target_and_separate_configured_manager_scope()
    {
        Assert.Equal(TaskTransitionDenial.TargetRequired, TaskLifecyclePolicy.EvaluateUser(TaskState.Draft, TaskState.Assigned, Authorized, Task, Valid).Denial);
        Assert.Equal(TaskTransitionDenial.TargetNotValidated, Evaluate(TaskState.Draft, TaskState.Assigned, Valid with { TargetValidated = false }).Denial);
        var denied = TaskLifecyclePolicy.EvaluateUser(TaskState.Draft, TaskState.Assigned,
            Authorized with { ManagedDepartmentIds = new HashSet<Guid>() }, Task, Valid, Department);
        Assert.Equal(TaskTransitionDenial.AccessDenied, denied.Denial); Assert.Equal(AccessDenial.ManagementScopeDenied, denied.AccessDenial);
        Assert.False(TaskLifecyclePolicy.EvaluateUser(TaskState.Draft, TaskState.Assigned, Authorized, Task, Valid, Guid.NewGuid()).Allowed);
    }

    [Theory]
    [InlineData(TaskState.Assigned, TaskState.Accepted)]
    [InlineData(TaskState.Assigned, TaskState.Rejected)]
    [InlineData(TaskState.Accepted, TaskState.InProgress)]
    [InlineData(TaskState.Overdue, TaskState.InProgress)]
    [InlineData(TaskState.InProgress, TaskState.Submitted)]
    public void Assigned_employee_relationship_is_required_even_with_tenant_permission(TaskState from, TaskState to) =>
        Assert.Equal(TaskTransitionDenial.AssignedTargetRequired, Evaluate(from, to, Valid with { IsAssignedTarget = false }).Denial);

    [Theory]
    [InlineData(TaskState.Confirmed)]
    [InlineData(TaskState.InProgress)]
    public void Submitted_result_review_requires_the_authorized_reviewer(TaskState next) =>
        Assert.Equal(TaskTransitionDenial.ReviewerRequired, Evaluate(TaskState.Submitted, next, Valid with { IsReviewer = false }).Denial);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public void Rejection_and_cancellation_require_a_reason(string? reason)
    {
        Assert.Equal(TaskTransitionDenial.ReasonRequired, Evaluate(TaskState.Assigned, TaskState.Rejected, Valid with { Reason = reason }).Denial);
        Assert.Equal(TaskTransitionDenial.ReasonRequired, Evaluate(TaskState.InProgress, TaskState.Cancelled, Valid with { Reason = reason }).Denial);
    }

    [Fact]
    public void Result_confirmation_and_completion_cannot_skip_required_evidence()
    {
        Assert.Equal(TaskTransitionDenial.SubmittedResultRequired, Evaluate(TaskState.InProgress, TaskState.Submitted, Valid with { HasSubmittedResult = false }).Denial);
        Assert.Equal(TaskTransitionDenial.SubmittedResultRequired, Evaluate(TaskState.Submitted, TaskState.Confirmed, Valid with { HasSubmittedResult = false }).Denial);
        Assert.Equal(TaskTransitionDenial.ResultAcceptanceRequired, Evaluate(TaskState.Submitted, TaskState.Confirmed, Valid with { ResultAccepted = false }).Denial);
        Assert.Equal(TaskTransitionDenial.ConfirmationRequired, Evaluate(TaskState.Confirmed, TaskState.Completed, Valid with { ConfirmationSatisfied = false }).Denial);
        foreach (var incomplete in new[] { Valid with { HasSubmittedResult = false }, Valid with { ConfirmationSatisfied = false }, Valid with { WorkflowAllowsTransition = false } })
            Assert.False(TaskLifecyclePolicy.EvaluateSystem(TaskState.Confirmed, TaskState.Completed, Tenant, true, incomplete).Allowed);
        Assert.False(Evaluate(TaskState.Submitted, TaskState.Completed).Allowed);
    }

    [Fact]
    public void Owner_approved_canonical_graph_requires_resume_before_submit_and_in_progress_rework()
    {
        Assert.Equal(TaskTransitionDenial.InvalidTransition, Evaluate(TaskState.Overdue, TaskState.Submitted).Denial);
        Assert.True(Evaluate(TaskState.Overdue, TaskState.InProgress).Allowed);
        Assert.True(Evaluate(TaskState.InProgress, TaskState.Submitted).Allowed);
        Assert.Equal(TaskTransitionDenial.InvalidTransition, Evaluate(TaskState.Submitted, TaskState.Rejected).Denial);
        Assert.True(Evaluate(TaskState.Submitted, TaskState.InProgress).Allowed);
        Assert.True(Evaluate(TaskState.Assigned, TaskState.Rejected).Allowed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(999)]
    public void Unknown_enum_values_fail_closed(int invalid)
    {
        Assert.Equal(TaskTransitionDenial.InvalidState, Evaluate((TaskState)invalid, TaskState.Assigned).Denial);
        Assert.Equal(TaskTransitionDenial.InvalidState, Evaluate(TaskState.Assigned, (TaskState)invalid).Denial);
        Assert.Equal(TaskTransitionDenial.InvalidState, TaskLifecyclePolicy.EvaluateSystem((TaskState)invalid, TaskState.Overdue, Tenant, true, Valid).Denial);
    }

    private static TaskTransitionDecision Evaluate(TaskState from, TaskState to, TaskTransitionEvidence? evidence = null) =>
        TaskLifecyclePolicy.EvaluateUser(from, to, Authorized, Task, evidence ?? Valid, Department);
}
