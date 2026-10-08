using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Organization;

namespace BizFlow.Domain.Workflows;

// Runtime evidence is deliberately not persisted and cannot be supplied by an API DTO.
public sealed class TaskWorkflowMutation
{
    public Guid ActorId { get; }
    public TaskState Before { get; }
    public TaskState After { get; }
    public DateTimeOffset BeforeUpdatedAt { get; }
    internal TaskWorkflowMutation(Guid actorId, TaskState before, TaskState after, DateTimeOffset beforeUpdatedAt)
    { ActorId = actorId; Before = before; After = after; BeforeUpdatedAt = beforeUpdatedAt; }
}

public interface IWorkflowEngine
{
    TaskTransitionDecision AssignTask(WorkTask task, AccessSnapshot access, Guid targetDepartmentId, bool targetValidated, DateTimeOffset now);
    TaskTransitionDecision AcceptTask(WorkTask task, TaskAssignment assignment, UserAccount recipient, AccessSnapshot access, DateTimeOffset now);
    TaskTransitionDecision StartTask(WorkTask task, TaskAssignment assignment, UserAccount performer, AccessSnapshot access, DateTimeOffset now);
    TaskTransitionDecision SubmitResult(WorkTask task, TaskAssignment assignment, UserAccount performer, AccessSnapshot access, DateTimeOffset now);
    TaskTransitionDecision ConfirmResult(WorkTask task, TaskAssignment assignment, UserAccount reviewer, AccessSnapshot access, bool accept, DateTimeOffset now);
}

public sealed class TaskWorkflowEngine : IWorkflowEngine
{
    public TaskTransitionDecision ConfirmResult(WorkTask task, TaskAssignment assignment, UserAccount reviewer, AccessSnapshot access, bool accept, DateTimeOffset now)
    {
        if (task.WorkflowVersionId is not null || task.SlaVersionId is not null || task.DeletedAt is not null)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.WorkflowDenied);
        if (task.Status != TaskState.Submitted) return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTransition);
        var targetState = accept ? TaskState.Confirmed : TaskState.InProgress;
        var decision = TaskLifecyclePolicy.EvaluateUser(task.Status, targetState, access,
            new(task.TenantId, task.CreatorId), new(WorkflowAllowsTransition: true, IsReviewer: true, HasSubmittedResult: true, ResultAccepted: accept));
        if (!decision.Allowed) return decision;
        if (now.ToUniversalTime() < task.UpdatedAt) throw new ArgumentOutOfRangeException(nameof(now));
        if (accept)
        {
            task.ApplyConfirmationTransition(new(access.UserId, task.Status, TaskState.Completed, task.UpdatedAt), now);
        }
        else
        {
            task.ApplyConfirmationTransition(new(access.UserId, task.Status, TaskState.InProgress, task.UpdatedAt), now);
        }
        return decision;
    }

    public TaskTransitionDecision SubmitResult(WorkTask task, TaskAssignment assignment, UserAccount performer, AccessSnapshot access, DateTimeOffset now)
    {
        if (task.WorkflowVersionId is not null || task.SlaVersionId is not null || task.DeletedAt is not null)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.WorkflowDenied);
        var target = assignment.TaskId == task.Id && assignment.EndedAt is null && assignment.AcceptedAt is not null && assignment.RejectedAt is null &&
            assignment.UserId == performer.Id && performer.Id == access.UserId && performer.TenantId == task.TenantId &&
            performer.Status == UserStatus.Active && performer.DeletedAt is null;
        if (task.Status != TaskState.InProgress) return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTransition);
        var decision = TaskLifecyclePolicy.EvaluateUser(task.Status, TaskState.Submitted, access,
            new(task.TenantId, task.CreatorId), new(WorkflowAllowsTransition: true, IsAssignedTarget: target, HasSubmittedResult: true));
        if (!decision.Allowed) return decision;
        if (now.ToUniversalTime() < task.UpdatedAt) throw new ArgumentOutOfRangeException(nameof(now));
        task.ApplySubmissionTransition(new(access.UserId, task.Status, TaskState.Submitted, task.UpdatedAt), now);
        return decision;
    }

    public TaskTransitionDecision StartTask(WorkTask task, TaskAssignment assignment, UserAccount performer, AccessSnapshot access, DateTimeOffset now)
    {
        if (task.WorkflowVersionId is not null || task.SlaVersionId is not null || task.DeletedAt is not null)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.WorkflowDenied);
        var target = assignment.TaskId == task.Id && assignment.EndedAt is null && assignment.AcceptedAt is not null && assignment.RejectedAt is null &&
            assignment.UserId == performer.Id && performer.Id == access.UserId && performer.TenantId == task.TenantId &&
            performer.Status == UserStatus.Active && performer.DeletedAt is null;
        // This entry point cannot invoke reviewer-driven SUBMITTED → IN_PROGRESS rework.
        if (task.Status is not (TaskState.Accepted or TaskState.Overdue)) return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTransition);
        var decision = TaskLifecyclePolicy.EvaluateUser(task.Status, TaskState.InProgress, access,
            new(task.TenantId, task.CreatorId), new(WorkflowAllowsTransition: true, IsAssignedTarget: target));
        if (!decision.Allowed) return decision;
        if (now.ToUniversalTime() < task.UpdatedAt || now.ToUniversalTime() < assignment.AcceptedAt)
            throw new ArgumentOutOfRangeException(nameof(now));
        task.ApplyExecutionTransition(new(access.UserId, task.Status, TaskState.InProgress, task.UpdatedAt), now);
        return decision;
    }

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
        task.ApplyAcceptanceTransition(new(access.UserId, task.Status, TaskState.Accepted, task.UpdatedAt), now);
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
            task.ApplyAssignmentTransition(new(access.UserId, task.Status, TaskState.Assigned, task.UpdatedAt), now);
        }
        return decision;
    }
}
