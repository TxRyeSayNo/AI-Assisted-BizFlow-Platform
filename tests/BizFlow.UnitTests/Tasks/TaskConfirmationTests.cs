using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskConfirmationTests
{
    [Fact]
    public void ConfirmResult_accept_transitions_submitted_task_to_confirmed_and_completed()
    {
        var (task, assignment, reviewer, access, now) = Fixture(TaskState.Submitted);
        var engine = new TaskWorkflowEngine();
        var decision = engine.ConfirmResult(task, assignment, reviewer, access, accept: true, now);

        Assert.True(decision.Allowed);
        Assert.Equal(TaskState.Completed, task.Status);
        Assert.Equal(now, task.CompletedAt);
        Assert.Equal(now, task.UpdatedAt);
        Assert.NotNull(task.WorkflowMutation);
        Assert.Equal(reviewer.Id, task.WorkflowMutation.ActorId);

        var confirmation = Confirmation.TaskResultConfirmed(task, reviewer.Id, "Great work!");
        Assert.Equal("RESULT", confirmation.MilestoneType);
        Assert.Equal("CONFIRMED", confirmation.Decision);
        Assert.Equal("Great work!", confirmation.Note);

        var audit = AuditLog.TaskResultConfirmed(task, assignment, confirmation, new string('a', 64), new string('b', 64));
        Assert.Equal("TASK.RESULT_CONFIRMED", audit.Action);
        Assert.Equal(reviewer.Id, audit.ActorId);
        Assert.Equal(task.Id, audit.ObjectId);
        Assert.Contains("COMPLETED", audit.AfterJson!);
    }

    [Fact]
    public void ConfirmResult_rework_transitions_submitted_task_back_to_in_progress()
    {
        var (task, assignment, reviewer, access, now) = Fixture(TaskState.Submitted);
        var engine = new TaskWorkflowEngine();
        var decision = engine.ConfirmResult(task, assignment, reviewer, access, accept: false, now);

        Assert.True(decision.Allowed);
        Assert.Equal(TaskState.InProgress, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Equal(now, task.UpdatedAt);
        Assert.NotNull(task.WorkflowMutation);
        Assert.Equal(reviewer.Id, task.WorkflowMutation.ActorId);
        Assert.Equal(TaskState.Submitted, task.WorkflowMutation.Before);
        Assert.Equal(TaskState.InProgress, task.WorkflowMutation.After);

        var confirmation = Confirmation.TaskResultRejected(task, reviewer.Id, "Please fix the formatting.");
        Assert.Equal("RESULT", confirmation.MilestoneType);
        Assert.Equal("REJECTED", confirmation.Decision);
        Assert.Equal("Please fix the formatting.", confirmation.Note);

        var audit = AuditLog.TaskResultRework(task, assignment, confirmation, new string('a', 64), new string('b', 64));
        Assert.Equal("TASK.RESULT_REWORK", audit.Action);
        Assert.Equal(reviewer.Id, audit.ActorId);
        Assert.Equal(task.Id, audit.ObjectId);
        Assert.Contains("IN_PROGRESS", audit.AfterJson!);
    }

    [Theory]
    [InlineData(TaskState.Draft)]
    [InlineData(TaskState.Assigned)]
    [InlineData(TaskState.Accepted)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Confirmed)]
    [InlineData(TaskState.Completed)]
    [InlineData(TaskState.Rejected)]
    [InlineData(TaskState.Cancelled)]
    public void ConfirmResult_denied_for_non_submitted_states(TaskState state)
    {
        var (task, assignment, reviewer, access, now) = Fixture(state);
        var engine = new TaskWorkflowEngine();
        var decision = engine.ConfirmResult(task, assignment, reviewer, access, accept: true, now);

        Assert.False(decision.Allowed);
        Assert.Equal(state, task.Status);
        Assert.Null(task.WorkflowMutation);
    }

    [Fact]
    public void ConfirmResult_requires_confirm_permission()
    {
        var (task, assignment, reviewer, access, now) = Fixture(TaskState.Submitted);
        var noPermAccess = access with { Grants = [] };

        var engine = new TaskWorkflowEngine();
        var decision = engine.ConfirmResult(task, assignment, reviewer, noPermAccess, accept: true, now);

        Assert.False(decision.Allowed);
        Assert.Equal(TaskState.Submitted, task.Status);
        Assert.Null(task.WorkflowMutation);
    }

    private static (WorkTask, TaskAssignment, UserAccount, AccessSnapshot, DateTimeOffset) Fixture(TaskState state)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Guid.NewGuid();
        var performer = UserAccount.CreateTenantUser(tenant, "E001", "employee@example.test", "Employee", "fixture-only", now.AddDays(-1));
        var reviewer = UserAccount.CreateTenantUser(tenant, "M001", "manager@example.test", "Manager", "fixture-only", now.AddDays(-1));
        var task = WorkTask.CreateDraft(tenant, reviewer.Id, "Review Test", now.AddMinutes(-20), deadline: now.AddHours(4));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Assigned);
        var assignment = TaskAssignment.Create(task.Id, task.CreatorId, null, performer.Id, now.AddMinutes(-15));
        assignment.Accept(task, performer, now.AddMinutes(-14));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, state);
        var access = new AccessSnapshot(reviewer.Id, tenant, true, true, [new("tasks.confirm", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        return (task, assignment, reviewer, access, now);
    }
}
