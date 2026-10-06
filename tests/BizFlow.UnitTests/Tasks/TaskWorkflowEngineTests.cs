using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskWorkflowEngineTests
{
    [Theory]
    [InlineData(nameof(WorkTask.WorkflowVersionId))]
    [InlineData(nameof(WorkTask.SlaVersionId))]
    public void Pinned_configuration_never_silently_falls_back_to_default(string property)
    {
        var task = WorkTask.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Pinned", DateTimeOffset.UtcNow);
        typeof(WorkTask).GetProperty(property)!.SetValue(task, Guid.NewGuid());
        var department = Guid.NewGuid();
        var access = new AccessSnapshot(task.CreatorId, task.TenantId, true, true, [new("tasks.assign", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid> { department });
        Assert.Equal(TaskTransitionDenial.WorkflowDenied, new TaskWorkflowEngine().AssignTask(task, access, department, true, DateTimeOffset.UtcNow).Denial);
        Assert.Equal(TaskState.Draft, task.Status); Assert.Null(task.WorkflowMutation);
    }
    [Fact]
    public void Unpinned_assignment_uses_canonical_gate_and_creates_actor_bound_nonpersisted_evidence()
    {
        var now = DateTimeOffset.UtcNow; var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var department = Guid.NewGuid();
        var task = WorkTask.CreateDraft(tenant, user, "Work", now);
        var access = new AccessSnapshot(user, tenant, true, true, [new("tasks.assign", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid> { department });
        var engine = new TaskWorkflowEngine();
        Assert.False(engine.AssignTask(task, access, Guid.NewGuid(), true, now.AddMinutes(1)).Allowed);
        Assert.Equal(TaskState.Draft, task.Status); Assert.Null(task.WorkflowMutation);
        Assert.False(engine.AssignTask(task, access, department, false, now.AddMinutes(1)).Allowed);
        Assert.True(engine.AssignTask(task, access, department, true, now.AddMinutes(1)).Allowed);
        Assert.Equal(TaskState.Assigned, task.Status); Assert.Equal(user, task.WorkflowMutation!.ActorId);
        Assert.Equal(TaskState.Draft, task.WorkflowMutation.Before); Assert.Equal(now, task.WorkflowMutation.BeforeUpdatedAt);
        Assert.False(engine.AssignTask(task, access, department, true, now.AddMinutes(2)).Allowed);
    }
}
