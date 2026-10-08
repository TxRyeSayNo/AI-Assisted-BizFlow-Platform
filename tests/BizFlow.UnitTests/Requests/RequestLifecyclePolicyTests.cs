using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestLifecyclePolicyTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Department = Guid.NewGuid();
    private static readonly ResourceScope Request = new(Tenant, User, Department, [User]);

    private static readonly AccessSnapshot Authorized = new(User, Tenant, true, true,
        [
            new("requests.create", PermissionScope.Tenant),
            new("requests.submit", PermissionScope.Tenant),
            new("requests.route", PermissionScope.Tenant),
            new("requests.receive", PermissionScope.Tenant),
            new("requests.process", PermissionScope.Tenant),
            new("requests.resolve", PermissionScope.Tenant),
            new("requests.confirm", PermissionScope.Tenant),
            new("requests.reject", PermissionScope.Tenant),
            new("requests.cancel", PermissionScope.Tenant)
        ],
        new HashSet<Guid> { Department }, new HashSet<Guid> { Department });

    private static readonly RequestTransitionEvidence Valid = new(
        WorkflowAllowsTransition: true,
        TargetValidated: true,
        IsAssignedTarget: true,
        IsRequester: true,
        HasResolution: true,
        ResolutionAccepted: true,
        ConfirmationSatisfied: true,
        DeadlineOrSlaExceeded: true,
        Reason: "Valid reason");

    private static RequestTransitionDecision Evaluate(RequestState from, RequestState to,
        RequestTransitionEvidence? evidence = null, AccessSnapshot? access = null, Guid? targetDept = null) =>
        RequestLifecyclePolicy.EvaluateUser(from, to, access ?? Authorized, Request,
            evidence ?? Valid, targetDept ?? Department);

    [Fact]
    public void Exact_canonical_states_and_every_user_state_pair_are_checked()
    {
        Assert.Equal(12, Enum.GetValues<RequestState>().Length);

        HashSet<(RequestState, RequestState)> allowed =
        [
            (RequestState.Draft, RequestState.Submitted),
            (RequestState.Submitted, RequestState.Routed),
            (RequestState.Routed, RequestState.Received),
            (RequestState.Received, RequestState.InProgress),
            (RequestState.InProgress, RequestState.WaitingForInformation),
            (RequestState.WaitingForInformation, RequestState.InProgress),
            (RequestState.InProgress, RequestState.Resolved),
            (RequestState.Resolved, RequestState.Confirmed),
            (RequestState.Resolved, RequestState.InProgress), // ADR-0009
            (RequestState.Confirmed, RequestState.Closed),
            (RequestState.Submitted, RequestState.Rejected),
            (RequestState.Routed, RequestState.Rejected),
            (RequestState.Overdue, RequestState.InProgress) // ADR-0009
        ];

        foreach (var from in Enum.GetValues<RequestState>())
        {
            if (from is not (RequestState.Closed or RequestState.Cancelled or RequestState.Rejected))
                allowed.Add((from, RequestState.Cancelled));

            foreach (var to in Enum.GetValues<RequestState>())
            {
                var expected = allowed.Contains((from, to));
                var actual = Evaluate(from, to).Allowed;
                Assert.True(expected == actual, $"Transition {from} -> {to} expected {expected} but was {actual}");
            }
        }
    }

    [Fact]
    public void Only_system_overdue_detection_and_confirmed_closure_are_system_edges()
    {
        foreach (var from in Enum.GetValues<RequestState>())
        {
            foreach (var to in Enum.GetValues<RequestState>())
            {
                var expected = ((from is RequestState.Submitted or RequestState.Routed or RequestState.Received or RequestState.InProgress or RequestState.WaitingForInformation) && to == RequestState.Overdue) ||
                    (from == RequestState.Confirmed && to == RequestState.Closed);

                Assert.Equal(expected, RequestLifecyclePolicy.EvaluateSystem(from, to, Tenant, true, Valid).Allowed);
            }
        }

        Assert.Equal(RequestTransitionDenial.SystemOnly, Evaluate(RequestState.InProgress, RequestState.Overdue).Denial);
        Assert.Equal(RequestTransitionDenial.ThresholdNotCrossed, RequestLifecyclePolicy.EvaluateSystem(
            RequestState.InProgress, RequestState.Overdue, Tenant, true, Valid with { DeadlineOrSlaExceeded = false }).Denial);
        Assert.Equal(RequestTransitionDenial.InactiveTenant, RequestLifecyclePolicy.EvaluateSystem(RequestState.InProgress, RequestState.Overdue, Tenant, false, Valid).Denial);
        Assert.Equal(RequestTransitionDenial.InvalidTenant, RequestLifecyclePolicy.EvaluateSystem(RequestState.InProgress, RequestState.Overdue, Guid.Empty, true, Valid).Denial);
    }

    [Fact]
    public void Workflow_denial_blocks_allowed_transitions()
    {
        var denied = Evaluate(RequestState.Draft, RequestState.Submitted, Valid with { WorkflowAllowsTransition = false });
        Assert.False(denied.Allowed);
        Assert.Equal(RequestTransitionDenial.WorkflowDenied, denied.Denial);
    }

    [Fact]
    public void Rejection_requires_non_whitespace_reason()
    {
        var denied = Evaluate(RequestState.Submitted, RequestState.Rejected, Valid with { Reason = "   " });
        Assert.False(denied.Allowed);
        Assert.Equal(RequestTransitionDenial.ReasonRequired, denied.Denial);
    }

    [Fact]
    public void Resolution_requires_resolution_evidence()
    {
        var denied = Evaluate(RequestState.InProgress, RequestState.Resolved, Valid with { HasResolution = false });
        Assert.False(denied.Allowed);
        Assert.Equal(RequestTransitionDenial.ResolutionRequired, denied.Denial);
    }
}
