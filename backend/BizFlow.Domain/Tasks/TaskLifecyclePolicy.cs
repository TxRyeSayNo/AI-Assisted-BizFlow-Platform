using BizFlow.Domain.Security;

namespace BizFlow.Domain.Tasks;

// SSS §9.1: no extra business states. Persistence mapping belongs to the task slice.
public enum TaskState { Draft, Assigned, Accepted, InProgress, Submitted, Confirmed, Completed, Rejected, Cancelled, Overdue }

public enum TaskTransitionDenial
{
    None, InvalidState, InvalidTransition, InvalidTenant,
    AccessDenied, SystemOnly, InactiveTenant, WorkflowDenied, TargetRequired,
    TargetNotValidated, AssignedTargetRequired, ReviewerRequired, ReasonRequired,
    SubmittedResultRequired, ResultAcceptanceRequired, ConfirmationRequired, ThresholdNotCrossed
}

// Server-resolved evidence, not a command/HTTP DTO. WorkflowAllowsTransition means the
// pinned version (or approved default workflow) and all configured guards have passed.
public sealed record TaskTransitionEvidence(
    bool WorkflowAllowsTransition = false,
    bool TargetValidated = false,
    bool IsAssignedTarget = false,
    bool IsReviewer = false,
    bool HasSubmittedResult = false,
    bool ResultAccepted = false,
    bool ConfirmationSatisfied = false,
    bool DeadlineOrSlaExceeded = false,
    string? Reason = null);

public readonly record struct TaskTransitionDecision(bool Allowed, TaskTransitionDenial Denial, AccessDenial? AccessDenial = null)
{
    public static TaskTransitionDecision Permit => new(true, TaskTransitionDenial.None);
    public static TaskTransitionDecision Deny(TaskTransitionDenial denial) => new(false, denial);
}

/// <summary>
/// Canonical task lifecycle gate for the future workflow engine. It does not mutate state,
/// load records, persist evidence or replace Application authorization/transaction/audit.
/// Actors are capabilities and resource relationships, never role display names.
/// </summary>
public static class TaskLifecyclePolicy
{
    public static TaskTransitionDecision EvaluateUser(TaskState current, TaskState next,
        AccessSnapshot access, ResourceScope task, TaskTransitionEvidence evidence,
        Guid? managementTargetDepartmentId = null)
    {
        var edge = ValidateEdge(current, next);
        if (edge is { } invalid) return invalid;
        if (task.TenantId is null || task.TenantId == Guid.Empty)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTenant);
        if (next == TaskState.Overdue) return TaskTransitionDecision.Deny(TaskTransitionDenial.SystemOnly);

        var permission = next switch
        {
            TaskState.Assigned => "tasks.assign",
            TaskState.Accepted or TaskState.Rejected => "tasks.accept",
            TaskState.InProgress when current == TaskState.Submitted => "tasks.confirm",
            TaskState.InProgress => "tasks.execute",
            TaskState.Submitted => "tasks.submit",
            TaskState.Confirmed or TaskState.Completed => "tasks.confirm",
            TaskState.Cancelled => "tasks.cancel",
            _ => throw new InvalidOperationException("Every canonical user edge must map to a permission.")
        };
        var accessDecision = ResourcePolicy.Evaluate(access, permission, task,
            next == TaskState.Assigned ? managementTargetDepartmentId : null);
        if (!accessDecision.Allowed)
            return new(false, TaskTransitionDenial.AccessDenied, accessDecision.Denial);

        if (next == TaskState.Assigned)
        {
            if (managementTargetDepartmentId is null)
                return TaskTransitionDecision.Deny(TaskTransitionDenial.TargetRequired);
            if (!evidence.TargetValidated)
                return TaskTransitionDecision.Deny(TaskTransitionDenial.TargetNotValidated);
        }
        if ((current == TaskState.Assigned && (next is TaskState.Accepted or TaskState.Rejected)) ||
            (next == TaskState.InProgress && (current is TaskState.Accepted or TaskState.Overdue)) ||
            next == TaskState.Submitted)
        {
            if (!evidence.IsAssignedTarget)
                return TaskTransitionDecision.Deny(TaskTransitionDenial.AssignedTargetRequired);
        }
        if (current == TaskState.Submitted && (next is TaskState.Confirmed or TaskState.InProgress) && !evidence.IsReviewer)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.ReviewerRequired);
        if ((next is TaskState.Rejected or TaskState.Cancelled) && string.IsNullOrWhiteSpace(evidence.Reason))
            return TaskTransitionDecision.Deny(TaskTransitionDenial.ReasonRequired);
        return ValidateEvidence(next, evidence);
    }

    // Called only by trusted Application/background services, never a client-selected actor flag.
    // AI-assisted commands use EvaluateUser under the initiating user's live authority.
    public static TaskTransitionDecision EvaluateSystem(TaskState current, TaskState next,
        Guid tenantId, bool isTenantActive, TaskTransitionEvidence evidence)
    {
        var edge = ValidateEdge(current, next);
        if (edge is { } invalid) return invalid;
        if (tenantId == Guid.Empty) return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTenant);
        if (!isTenantActive) return TaskTransitionDecision.Deny(TaskTransitionDenial.InactiveTenant);
        if (next != TaskState.Overdue && !(current == TaskState.Confirmed && next == TaskState.Completed))
            return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTransition);
        if (next == TaskState.Overdue && !evidence.DeadlineOrSlaExceeded)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.ThresholdNotCrossed);
        return ValidateEvidence(next, evidence);
    }

    private static TaskTransitionDecision? ValidateEdge(TaskState current, TaskState next)
    {
        if (!Enum.IsDefined(current) || !Enum.IsDefined(next))
            return TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidState);
        // ADR-0003: owner confirmed exact §9.1 precedence over conflicting FR wording.
        // Overdue work resumes before submission; rejected results use InProgress rework.
        var allowed = (current, next) switch
        {
            (TaskState.Draft or TaskState.Rejected, TaskState.Assigned) => true,
            (TaskState.Assigned, TaskState.Accepted or TaskState.Rejected) => true,
            (TaskState.Accepted or TaskState.Overdue, TaskState.InProgress) => true,
            (TaskState.InProgress, TaskState.Submitted) => true,
            (TaskState.Submitted, TaskState.Confirmed or TaskState.InProgress) => true,
            (TaskState.Confirmed, TaskState.Completed) => true,
            (TaskState.InProgress or TaskState.Submitted, TaskState.Overdue) => true,
            (not (TaskState.Completed or TaskState.Cancelled), TaskState.Cancelled) => true,
            _ => false
        };
        return allowed ? null : TaskTransitionDecision.Deny(TaskTransitionDenial.InvalidTransition);
    }

    private static TaskTransitionDecision ValidateEvidence(TaskState next, TaskTransitionEvidence evidence)
    {
        if (!evidence.WorkflowAllowsTransition) return TaskTransitionDecision.Deny(TaskTransitionDenial.WorkflowDenied);
        if ((next is TaskState.Submitted or TaskState.Confirmed or TaskState.Completed) && !evidence.HasSubmittedResult)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.SubmittedResultRequired);
        if (next == TaskState.Confirmed && !evidence.ResultAccepted)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.ResultAcceptanceRequired);
        if (next == TaskState.Completed && !evidence.ConfirmationSatisfied)
            return TaskTransitionDecision.Deny(TaskTransitionDenial.ConfirmationRequired);
        return TaskTransitionDecision.Permit;
    }
}
