using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Organization;

namespace BizFlow.Domain.Workflows;

// Runtime evidence is deliberately not persisted and cannot be supplied by an API DTO.
public sealed class TaskWorkflowMutation
{
    public Guid ActorId { get; }
    public TaskState Before { get; }
    public DateTimeOffset BeforeUpdatedAt { get; }
    internal TaskWorkflowMutation(Guid actorId, TaskState before, DateTimeOffset beforeUpdatedAt)
    { ActorId = actorId; Before = before; BeforeUpdatedAt = beforeUpdatedAt; }
}

public interface IWorkflowEngine
{
    TaskTransitionDecision AssignTask(WorkTask task, AccessSnapshot access, Guid targetDepartmentId, bool targetValidated, DateTimeOffset now);
    TaskTransitionDecision AcceptTask(WorkTask task, TaskAssignment assignment, UserAccount recipient, AccessSnapshot access, DateTimeOffset now);
}

public sealed class TaskWorkflowEngine : IWorkflowEngine
{
    public TaskTransitionDecision AcceptTask(WorkTask task, TaskAssignment assignment, UserAccount recipient, AccessSnapshot access, DateTimeOffset now)
    {
        if (task.WorkflowVersionId is not null || task.SlaVersionId is not null || task.DeletedAt is not null)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.WorkflowDenied);
        var target = assignment.TaskId == task.Id && assignment.EndedAt is null && assignment.AcceptedAt is null && assignment.RejectedAt is null &&
            recipient.Id == access.UserId && recipient.TenantId == task.TenantId && recipient.Status == UserStatus.Active && recipient.DeletedAt is null &&
            (assignment.UserId is { } user ? user == recipient.Id : assignment.DepartmentId is { } department && department == recipient.DepartmentId);
        var decision = TaskLifecyclePolicy.EvaluateUser(task.Status, TaskState.Accepted, access,
            new(task.TenantId, task.CreatorId), new(WorkflowAllowsTransition: true, IsAssignedTarget: target));
        if (!decision.Allowed) return decision;
        if (now.ToUniversalTime() < task.UpdatedAt) throw new ArgumentOutOfRangeException(nameof(now));
        if (task.WorkflowMutation is not null) throw new InvalidOperationException("Reload the task before another transition.");
        assignment.Accept(task, recipient, now);
        task.ApplyAcceptanceTransition(new(access.UserId, task.Status, task.UpdatedAt), now);
        return decision;
    }

    public TaskTransitionDecision AssignTask(WorkTask task, AccessSnapshot access, Guid targetDepartmentId, bool targetValidated, DateTimeOffset now)
    {
        if (task.WorkflowVersionId is not null || task.SlaVersionId is not null || task.DeletedAt is not null)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.WorkflowDenied);
        var decision = TaskLifecyclePolicy.EvaluateUser(task.Status, TaskState.Assigned, access,
            new(task.TenantId, task.CreatorId), new(WorkflowAllowsTransition: true, TargetValidated: targetValidated), targetDepartmentId);
        if (decision.Allowed)
        {
            if (now.ToUniversalTime() < task.UpdatedAt) throw new ArgumentOutOfRangeException(nameof(now));
            task.ApplyAssignmentTransition(new(access.UserId, task.Status, task.UpdatedAt), now);
        }
        return decision;
    }
}
