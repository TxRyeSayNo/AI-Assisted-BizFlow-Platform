using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Acceptance_preserves_origin_and_records_actor_receipt_confirmation_and_audit(bool queue)
    {
        var (task, assignment, user, access, now) = Fixture(queue);
        var assignedBy = assignment.AssignedBy; var assignedAt = assignment.AssignedAt; var department = assignment.DepartmentId;
        Assert.True(new TaskWorkflowEngine().AcceptTask(task, assignment, user, access, now).Allowed);
        Assert.Equal(TaskState.Accepted, task.Status); Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal(assignedBy, assignment.AssignedBy); Assert.Equal(assignedAt, assignment.AssignedAt); Assert.Equal(department, assignment.DepartmentId);
        var confirmation = Confirmation.TaskAccepted(task, assignment, user.Id, " Ready ");
        Assert.Equal("TASK", confirmation.ObjectType); Assert.Equal("RECEIVE", confirmation.MilestoneType); Assert.Equal("CONFIRMED", confirmation.Decision);
        Assert.Equal(user.Id, confirmation.ActorId); Assert.Equal("Ready", confirmation.Note); Assert.Equal(now, confirmation.ConfirmedAt);
        var audit = AuditLog.TaskAccepted(task, assignment, confirmation, new string('a', 64), new string('b', 64));
        Assert.Equal(user.Id, audit.ActorId); Assert.Equal("TASK.ACCEPTED", audit.Action);
        Assert.False(new TaskWorkflowEngine().AcceptTask(task, assignment, user, access, now).Allowed);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("foreign")]
    [InlineData("membership")]
    [InlineData("workflow")]
    [InlineData("sla")]
    [InlineData("state")]
    public void Invalid_authority_or_configuration_cannot_claim_or_change_state(string scenario)
    {
        var (task, assignment, user, access, now) = Fixture(true);
        if (scenario == "permission") access = access with { Grants = [] };
        if (scenario == "foreign") access = access with { TenantId = Guid.NewGuid() };
        if (scenario == "membership") typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(user, Guid.NewGuid());
        if (scenario == "workflow") typeof(WorkTask).GetProperty(nameof(WorkTask.WorkflowVersionId))!.SetValue(task, Guid.NewGuid());
        if (scenario == "sla") typeof(WorkTask).GetProperty(nameof(WorkTask.SlaVersionId))!.SetValue(task, Guid.NewGuid());
        if (scenario == "state") typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Cancelled);
        var before = task.Status;
        Assert.False(new TaskWorkflowEngine().AcceptTask(task, assignment, user, access, now).Allowed);
        Assert.Null(assignment.UserId); Assert.Null(assignment.AcceptedAt); Assert.Null(task.WorkflowMutation); Assert.Equal(before, task.Status);
    }

    private static (WorkTask, TaskAssignment, UserAccount, AccessSnapshot, DateTimeOffset) Fixture(bool queue)
    {
        var now = DateTimeOffset.UtcNow; var tenant = Guid.NewGuid(); var department = Guid.NewGuid();
        var task = WorkTask.CreateDraft(tenant, Guid.NewGuid(), "Work", now.AddMinutes(-2));
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Assigned);
        var user = UserAccount.CreateTenantUser(tenant, "E001", "employee@example.test", "Employee", "test-only", now.AddDays(-1), department);
        var assignment = TaskAssignment.Create(task.Id, task.CreatorId, queue ? department : null, queue ? null : user.Id, now.AddMinutes(-1));
        var access = new AccessSnapshot(user.Id, tenant, true, true, [new("tasks.accept", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        return (task, assignment, user, access, now);
    }
}
