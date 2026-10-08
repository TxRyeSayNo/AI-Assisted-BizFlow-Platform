using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskSubmissionTests
{
    [Fact]
    public void SubmitResult_transitions_in_progress_task_to_submitted_and_creates_audit()
    {
        var (task, assignment, user, access, now) = Fixture(TaskState.InProgress);
        var engine = new TaskWorkflowEngine();
        var decision = engine.SubmitResult(task, assignment, user, access, now);

        Assert.True(decision.Allowed);
        Assert.Equal(TaskState.Submitted, task.Status);
        Assert.Equal(now, task.UpdatedAt);
        Assert.NotNull(task.WorkflowMutation);
        Assert.Equal(user.Id, task.WorkflowMutation.ActorId);
        Assert.Equal(TaskState.InProgress, task.WorkflowMutation.Before);
        Assert.Equal(TaskState.Submitted, task.WorkflowMutation.After);

        var result = TaskResult.CreateSnapshot(task.Id, user.Id, "Completed analysis document.", 1, now);
        var audit = AuditLog.TaskResultSubmitted(task, assignment, result, new string('a', 64), new string('b', 64));

        Assert.Equal("TASK.RESULT_SUBMITTED", audit.Action);
        Assert.Equal(user.Id, audit.ActorId);
        Assert.Equal(task.Id, audit.ObjectId);
        Assert.Contains("SUBMITTED", audit.AfterJson!);
    }

    [Theory]
    [InlineData(TaskState.Draft)]
    [InlineData(TaskState.Assigned)]
    [InlineData(TaskState.Accepted)]
    [InlineData(TaskState.Submitted)]
    [InlineData(TaskState.Confirmed)]
    [InlineData(TaskState.Completed)]
    [InlineData(TaskState.Rejected)]
    [InlineData(TaskState.Cancelled)]
    public void SubmitResult_denied_for_non_in_progress_states(TaskState state)
    {
        var (task, assignment, user, access, now) = Fixture(state);
        var engine = new TaskWorkflowEngine();
        var decision = engine.SubmitResult(task, assignment, user, access, now);

        Assert.False(decision.Allowed);
        Assert.Equal(state, task.Status);
        Assert.Null(task.WorkflowMutation);
    }

    [Theory]
    [InlineData("no-permission")]
    [InlineData("wrong-tenant")]
    [InlineData("other-user")]
    [InlineData("inactive-user")]
    [InlineData("ended-assignment")]
    [InlineData("unaccepted-assignment")]
    public void SubmitResult_requires_active_performer_with_valid_assignment_and_permission(string scenario)
    {
        var (task, assignment, user, access, now) = Fixture(TaskState.InProgress);
        if (scenario == "no-permission") access = access with { Grants = [] };
        if (scenario == "wrong-tenant") access = access with { TenantId = Guid.NewGuid() };
        if (scenario == "other-user") typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.UserId))!.SetValue(assignment, Guid.NewGuid());
        if (scenario == "inactive-user") typeof(UserAccount).GetProperty(nameof(UserAccount.Status))!.SetValue(user, UserStatus.Inactive);
        if (scenario == "ended-assignment") typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.EndedAt))!.SetValue(assignment, now);
        if (scenario == "unaccepted-assignment") typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.AcceptedAt))!.SetValue(assignment, null);

        var engine = new TaskWorkflowEngine();
        var decision = engine.SubmitResult(task, assignment, user, access, now);

        Assert.False(decision.Allowed);
        Assert.Equal(TaskState.InProgress, task.Status);
        Assert.Null(task.WorkflowMutation);
    }

    private static (WorkTask, TaskAssignment, UserAccount, AccessSnapshot, DateTimeOffset) Fixture(TaskState state)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Guid.NewGuid();
        var user = UserAccount.CreateTenantUser(tenant, "E001", "employee@example.test", "Employee", "fixture-only", now.AddDays(-1));
        var task = WorkTask.CreateDraft(tenant, Guid.NewGuid(), "Submit Test", now.AddMinutes(-10), deadline: now.AddHours(2));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Assigned);
        var assignment = TaskAssignment.Create(task.Id, task.CreatorId, null, user.Id, now.AddMinutes(-5));
        assignment.Accept(task, user, now.AddMinutes(-4));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, state);
        var access = new AccessSnapshot(user.Id, tenant, true, true, [new("tasks.submit", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        return (task, assignment, user, access, now);
    }
}
