using BizFlow.Domain.Security;

namespace BizFlow.Domain.Requests;

public enum RequestTransitionDenial
{
    None,
    InvalidState,
    InvalidTransition,
    InvalidTenant,
    AccessDenied,
    SystemOnly,
    InactiveTenant,
    WorkflowDenied,
    TargetRequired,
    TargetNotValidated,
    AssignedTargetRequired,
    RequesterRequired,
    ReasonRequired,
    ResolutionRequired,
    ConfirmationRequired,
    ThresholdNotCrossed
}

public sealed record RequestTransitionEvidence(
    bool WorkflowAllowsTransition = false,
    bool TargetValidated = false,
    bool IsAssignedTarget = false,
    bool IsRequester = false,
    bool HasResolution = false,
    bool ResolutionAccepted = false,
    bool ConfirmationSatisfied = false,
    bool DeadlineOrSlaExceeded = false,
    string? Reason = null);

public readonly record struct RequestTransitionDecision(bool Allowed, RequestTransitionDenial Denial, AccessDenial? AccessDenial = null)
{
    public static RequestTransitionDecision Permit => new(true, RequestTransitionDenial.None);
    public static RequestTransitionDecision Deny(RequestTransitionDenial denial) => new(false, denial);
}

/// <summary>
/// Canonical Request lifecycle gate based on SSS §9.2 and ADR-0009.
/// Evaluates user and system transitions against permissions, resource scopes, and evidence.
/// </summary>
public static class RequestLifecyclePolicy
{
    public static RequestTransitionDecision EvaluateUser(RequestState current, RequestState next,
        AccessSnapshot access, ResourceScope request, RequestTransitionEvidence evidence,
        Guid? managementTargetDepartmentId = null)
    {
        var edge = ValidateEdge(current, next);
        if (edge is { } invalid) return invalid;
        if (request.TenantId is null || request.TenantId == Guid.Empty)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.InvalidTenant);
        if (next == RequestState.Overdue)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.SystemOnly);

        var permission = next switch
        {
            RequestState.Submitted => "requests.submit",
            RequestState.Routed => "requests.route",
            RequestState.Received => "requests.receive",
            RequestState.InProgress when current == RequestState.Resolved => "requests.confirm",
            RequestState.InProgress => "requests.process",
            RequestState.WaitingForInformation => "requests.process",
            RequestState.Resolved => "requests.resolve",
            RequestState.Confirmed => "requests.confirm",
            RequestState.Closed => "requests.confirm",
            RequestState.Rejected => "requests.reject",
            RequestState.Cancelled => "requests.cancel",
            _ => throw new InvalidOperationException("Every canonical user edge must map to a permission.")
        };

        var accessDecision = ResourcePolicy.Evaluate(access, permission, request,
            next == RequestState.Routed ? managementTargetDepartmentId : null);
        if (!accessDecision.Allowed)
            return new(false, RequestTransitionDenial.AccessDenied, accessDecision.Denial);

        if (next == RequestState.Routed)
        {
            if (managementTargetDepartmentId is null)
                return RequestTransitionDecision.Deny(RequestTransitionDenial.TargetRequired);
            if (!evidence.TargetValidated)
                return RequestTransitionDecision.Deny(RequestTransitionDenial.TargetNotValidated);
        }

        if (next is RequestState.Received or RequestState.InProgress && current == RequestState.Routed)
        {
            if (!evidence.IsAssignedTarget)
                return RequestTransitionDecision.Deny(RequestTransitionDenial.AssignedTargetRequired);
        }

        if (current == RequestState.Resolved && (next is RequestState.Confirmed or RequestState.InProgress) && !evidence.IsRequester)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.RequesterRequired);

        if (next == RequestState.Rejected && string.IsNullOrWhiteSpace(evidence.Reason))
            return RequestTransitionDecision.Deny(RequestTransitionDenial.ReasonRequired);

        return ValidateEvidence(next, evidence);
    }

    public static RequestTransitionDecision EvaluateSystem(RequestState current, RequestState next,
        Guid tenantId, bool isTenantActive, RequestTransitionEvidence evidence)
    {
        var edge = ValidateEdge(current, next);
        if (edge is { } invalid) return invalid;
        if (tenantId == Guid.Empty) return RequestTransitionDecision.Deny(RequestTransitionDenial.InvalidTenant);
        if (!isTenantActive) return RequestTransitionDecision.Deny(RequestTransitionDenial.InactiveTenant);

        if (next != RequestState.Overdue && !(current == RequestState.Confirmed && next == RequestState.Closed))
            return RequestTransitionDecision.Deny(RequestTransitionDenial.InvalidTransition);

        if (next == RequestState.Overdue && !evidence.DeadlineOrSlaExceeded)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.ThresholdNotCrossed);

        return ValidateEvidence(next, evidence);
    }

    private static RequestTransitionDecision? ValidateEdge(RequestState current, RequestState next)
    {
        if (!Enum.IsDefined(current) || !Enum.IsDefined(next))
            return RequestTransitionDecision.Deny(RequestTransitionDenial.InvalidState);

        var allowed = (current, next) switch
        {
            (RequestState.Draft, RequestState.Submitted) => true,
            (RequestState.Submitted, RequestState.Routed) => true,
            (RequestState.Routed, RequestState.Received) => true,
            (RequestState.Received, RequestState.InProgress) => true,
            (RequestState.InProgress, RequestState.WaitingForInformation) => true,
            (RequestState.WaitingForInformation, RequestState.InProgress) => true,
            (RequestState.InProgress, RequestState.Resolved) => true,
            (RequestState.Resolved, RequestState.Confirmed) => true,
            (RequestState.Resolved, RequestState.InProgress) => true, // ADR-0009: requester rework
            (RequestState.Confirmed, RequestState.Closed) => true,
            (RequestState.Submitted or RequestState.Routed, RequestState.Rejected) => true,
            (RequestState.Submitted or RequestState.Routed or RequestState.Received or RequestState.InProgress or RequestState.WaitingForInformation, RequestState.Overdue) => true,
            (RequestState.Overdue, RequestState.InProgress) => true, // ADR-0009: resume work
            (not (RequestState.Closed or RequestState.Cancelled or RequestState.Rejected), RequestState.Cancelled) => true,
            _ => false
        };

        return allowed ? null : RequestTransitionDecision.Deny(RequestTransitionDenial.InvalidTransition);
    }

    private static RequestTransitionDecision ValidateEvidence(RequestState next, RequestTransitionEvidence evidence)
    {
        if (!evidence.WorkflowAllowsTransition)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.WorkflowDenied);
        if (next == RequestState.Resolved && !evidence.HasResolution)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.ResolutionRequired);
        if (next == RequestState.Closed && !evidence.ConfirmationSatisfied)
            return RequestTransitionDecision.Deny(RequestTransitionDenial.ConfirmationRequired);
        return RequestTransitionDecision.Permit;
    }
}
