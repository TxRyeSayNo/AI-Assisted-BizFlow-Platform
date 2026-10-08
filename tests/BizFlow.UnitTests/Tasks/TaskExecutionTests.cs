using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskExecutionTests
{
    [Theory]
    [InlineData(TaskState.Accepted)]
    [InlineData(TaskState.Overdue)]
    public void Start_and_resume_preserve_receipt_deadline_and_actor_bound_history(TaskState state)
    {
        var (task, assignment, user, access, now) = Fixture(state); var deadline = task.Deadline; var receipt = assignment.AcceptedAt;
        Assert.True(new TaskWorkflowEngine().StartTask(task, assignment, user, access, now).Allowed);
        Assert.Equal(TaskState.InProgress, task.Status); Assert.Equal(now, task.UpdatedAt); Assert.Equal(deadline, task.Deadline);
        Assert.Equal(receipt, assignment.AcceptedAt); Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal(state, task.WorkflowMutation!.Before); Assert.Equal(TaskState.InProgress, task.WorkflowMutation.After);
        var audit = AuditLog.TaskStarted(task, assignment, new string('a', 64), new string('b', 64));
        Assert.Equal(user.Id, audit.ActorId); Assert.Equal("TASK.STARTED", audit.Action); Assert.Contains(state.ToString().ToUpperInvariant(), audit.BeforeJson!);
    }
    [Theory]
    [InlineData(TaskState.Draft)]
    [InlineData(TaskState.Assigned)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Submitted)]
    [InlineData(TaskState.Confirmed)]
    [InlineData(TaskState.Completed)]
    [InlineData(TaskState.Rejected)]
    [InlineData(TaskState.Cancelled)]
    public void Start_cannot_invent_self_edges_or_invoke_reviewer_rework(TaskState state)
    {
        var (task, assignment, user, access, now) = Fixture(state);
        Assert.False(new TaskWorkflowEngine().StartTask(task, assignment, user, access, now).Allowed);
        Assert.Equal(state, task.Status); Assert.Null(task.WorkflowMutation);
    }
    [Theory]
    [InlineData("permission")]
    [InlineData("foreign")]
    [InlineData("other-user")]
    [InlineData("inactive")]
    [InlineData("unaccepted")]
    [InlineData("ended")]
    [InlineData("workflow")]
    [InlineData("sla")]
    public void Execution_requires_live_performer_receipt_authority_and_executable_configuration(string scenario)
    {
        var (task, assignment, user, access, now) = Fixture(TaskState.Accepted);
        if (scenario == "permission") access = access with { Grants = [] };
        if (scenario == "foreign") access = access with { TenantId = Guid.NewGuid() };
        if (scenario == "other-user") typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.UserId))!.SetValue(assignment, Guid.NewGuid());
        if (scenario == "inactive") typeof(UserAccount).GetProperty(nameof(UserAccount.Status))!.SetValue(user, UserStatus.Inactive);
        if (scenario == "unaccepted") typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.AcceptedAt))!.SetValue(assignment, null);
        if (scenario == "ended") typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.EndedAt))!.SetValue(assignment, now);
        if (scenario == "workflow") typeof(WorkTask).GetProperty(nameof(WorkTask.WorkflowVersionId))!.SetValue(task, Guid.NewGuid());
        if (scenario == "sla") typeof(WorkTask).GetProperty(nameof(WorkTask.SlaVersionId))!.SetValue(task, Guid.NewGuid());
        Assert.False(new TaskWorkflowEngine().StartTask(task, assignment, user, access, now).Allowed);
        Assert.Equal(TaskState.Accepted, task.Status); Assert.Null(task.WorkflowMutation);
    }
    private static (WorkTask, TaskAssignment, UserAccount, AccessSnapshot, DateTimeOffset) Fixture(TaskState state)
    {
        var now = DateTimeOffset.UtcNow; var tenant = Guid.NewGuid();
        var user = UserAccount.CreateTenantUser(tenant, "E001", "employee@example.test", "Employee", "fixture-only", now.AddDays(-1));
        var task = WorkTask.CreateDraft(tenant, Guid.NewGuid(), "Execute", now.AddMinutes(-2), deadline: now.AddHours(-1));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Assigned);
        var assignment = TaskAssignment.Create(task.Id, task.CreatorId, null, user.Id, now.AddMinutes(-2));
        assignment.Accept(task, user, now.AddMinutes(-1));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, state);
        var access = new AccessSnapshot(user.Id, tenant, true, true, [new("tasks.execute", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        return (task, assignment, user, access, now);
    }
}
