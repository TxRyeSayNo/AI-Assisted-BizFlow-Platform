using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using Xunit;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestLifecycleCommandTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _departmentId = Guid.NewGuid();

    [Fact]
    public void RequestRouting_Create_Validates_Targets_And_Fields()
    {
        var now = DateTimeOffset.UtcNow;
        var reqId = Guid.NewGuid();

        // Target required
        Assert.Throws<ArgumentException>(() =>
            RequestRouting.Create(_tenantId, reqId, _managerId, now, null, null));

        // Valid department target
        var routing = RequestRouting.Create(_tenantId, reqId, _managerId, now, _departmentId, reason: "Route to IT");
        Assert.Equal(_departmentId, routing.ToDepartmentId);
        Assert.Null(routing.ToUserId);
        Assert.Equal("Route to IT", routing.Reason);
        Assert.Equal(RequestRoutingSource.Manual, routing.Source);

        // Valid user target with AI source
        var userRouting = RequestRouting.Create(_tenantId, reqId, _managerId, now, null, _employeeId, source: RequestRoutingSource.Ai);
        Assert.Equal(_employeeId, userRouting.ToUserId);
        Assert.Equal(RequestRoutingSource.Ai, userRouting.Source);
    }

    [Fact]
    public void Confirmation_RequestReceived_Enforces_Routed_Transition()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _employeeId, RequestState.Routed, now);
        request.ApplyReceiptTransition(new(_employeeId, RequestState.Routed, RequestState.Received, request.UpdatedAt), now);

        var confirmation = Confirmation.RequestReceived(request, _employeeId, "Received note");
        Assert.Equal("REQUEST", confirmation.ObjectType);
        Assert.Equal("RECEIVE", confirmation.MilestoneType);
        Assert.Equal("CONFIRMED", confirmation.Decision);
        Assert.Equal(_employeeId, confirmation.ActorId);
        Assert.Equal("Received note", confirmation.Note);

        // Invalid actor throws
        Assert.Throws<InvalidOperationException>(() =>
            Confirmation.RequestReceived(request, Guid.NewGuid(), null));
    }

    [Fact]
    public void AuditLog_RequestLifecycle_Factories_Enforce_Matching_State()
    {
        var now = DateTimeOffset.UtcNow;
        var req1 = RequestInState(_tenantId, _employeeId, RequestState.Submitted, now);
        req1.ApplyRoutingTransition(new(_managerId, RequestState.Submitted, RequestState.Routed, req1.UpdatedAt), now);

        var routing = RequestRouting.Create(_tenantId, req1.Id, _managerId, now, _departmentId);
        var routedAudit = AuditLog.RequestRouted(req1, routing, now);
        Assert.Equal("REQUEST.ROUTED", routedAudit.Action);
        Assert.Equal(_managerId, routedAudit.ActorId);

        var req2 = RequestInState(_tenantId, _employeeId, RequestState.Routed, now);
        req2.ApplyReceiptTransition(new(_employeeId, RequestState.Routed, RequestState.Received, req2.UpdatedAt), now);
        var confirmation = Confirmation.RequestReceived(req2, _employeeId, null);
        var receivedAudit = AuditLog.RequestReceived(req2, confirmation, now);
        Assert.Equal("REQUEST.RECEIVED", receivedAudit.Action);

        var req3 = RequestInState(_tenantId, _employeeId, RequestState.Received, now);
        req3.ApplyExecutionTransition(new(_employeeId, RequestState.Received, RequestState.InProgress, req3.UpdatedAt), now);
        var startedAudit = AuditLog.RequestExecutionStarted(req3, now);
        Assert.Equal("REQUEST.STARTED", startedAudit.Action);

        // Rejection audit
        var req4 = RequestInState(_tenantId, _employeeId, RequestState.Submitted, now);
        req4.ApplyRejectionTransition(new(_managerId, RequestState.Submitted, RequestState.Rejected, req4.UpdatedAt), now);
        var rejectedAudit = AuditLog.RequestRejected(req4, "Not actionable", now);
        Assert.Equal("REQUEST.REJECTED", rejectedAudit.Action);

        // Cancellation audit
        var req5 = RequestInState(_tenantId, _employeeId, RequestState.Submitted, now);
        req5.ApplyCancellationTransition(new(_employeeId, RequestState.Submitted, RequestState.Cancelled, req5.UpdatedAt), now);
        var cancelledAudit = AuditLog.RequestCancelled(req5, now);
        Assert.Equal("REQUEST.CANCELLED", cancelledAudit.Action);
    }

    [Fact]
    public async Task RequestRoutingCommand_Validates_Target_Existence()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _employeeId, RequestState.Submitted, now);
        var store = new FakeRoutingStore(request, deptExists: false);

        var command = new RequestRoutingCommand(
            new FakeContext(_tenantId, _managerId),
            new PassAuthorizer(),
            new PassSnapshots(_tenantId, _managerId, ["requests.route"]),
            new FakeAudit(),
            store,
            TimeProvider.System,
            new FakeUpdates());

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() =>
            command.RouteAsync(request.Id, new(_departmentId, null), null, CancellationToken.None));

        Assert.Equal("REQUEST.INVALID_TARGET", ex.Code);
    }

    [Fact]
    public async Task RequestReceiptCommand_Rejects_Non_Assigned_Target()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _employeeId, RequestState.Routed, now);

        var otherUser = Guid.NewGuid();
        var routing = RequestRouting.Create(_tenantId, request.Id, _managerId, now, _departmentId, toUserId: otherUser);
        var store = new FakeReceiptStore(request, routing, isUserInDept: false);

        var command = new RequestReceiptCommand(
            new FakeContext(_tenantId, _employeeId),
            new PassAuthorizer(),
            new PassSnapshots(_tenantId, _employeeId, ["requests.receive"]),
            new FakeAudit(),
            store,
            TimeProvider.System,
            new FakeUpdates());

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() =>
            command.ReceiveAsync(request.Id, new("My note"), null, CancellationToken.None));

        Assert.Equal("REQUEST.NOT_ASSIGNED_TARGET", ex.Code);
    }

    [Fact]
    public async Task RequestRejectionCommand_Requires_Non_Empty_Reason()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _employeeId, RequestState.Submitted, now);
        var store = new FakeRejectionStore(request);

        var command = new RequestRejectionCommand(
            new FakeContext(_tenantId, _managerId),
            new PassAuthorizer(),
            new PassSnapshots(_tenantId, _managerId, ["requests.reject"]),
            new FakeAudit(),
            store,
            TimeProvider.System,
            new FakeUpdates());

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() =>
            command.RejectAsync(request.Id, new("   "), null, CancellationToken.None));

        Assert.Equal("REQUEST.REASON_REQUIRED", ex.Code);
    }

    private static WorkRequest RequestInState(Guid tenantId, Guid requesterId, RequestState state, DateTimeOffset now)
    {
        var req = WorkRequest.CreateSubmitted(tenantId, requesterId, Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", now);
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(req, state);
        return req;
    }

    private sealed class FakeContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
    }

    private sealed class PassAuthorizer : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope scope, Guid? targetDepartmentId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class PassSnapshots(Guid tenantId, Guid userId, string[] permissions, Guid? departmentId = null) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid uid, Guid? tid, CancellationToken cancellationToken) =>
            Task.FromResult<AccessSnapshot?>(new(
                uid == Guid.Empty ? userId : uid,
                tid ?? tenantId,
                true,
                true,
                permissions.Select(p => new PermissionGrant(p, PermissionScope.Tenant)).ToHashSet(),
                departmentId is { } d ? new HashSet<Guid> { d } : new HashSet<Guid>(),
                departmentId is { } m ? new HashSet<Guid> { m } : new HashSet<Guid>()));
    }

    private sealed class FakeAudit : ISecurityAuditWriter
    {
        public Task RecordDeniedAccessAsync(Guid? actorId, Guid? tenantId, string permission, AccessDenial denial, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeUpdates : INotificationUpdates
    {
        public Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeRoutingStore(WorkRequest request, bool deptExists) : IRequestRoutingStore, IRequestRoutingTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => null;
        public Task<IRequestRoutingTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken) =>
            Task.FromResult<IRequestRoutingTransaction>(this);
        public Task<bool> DepartmentExistsAndActiveAsync(Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(deptExists);
        public Task<bool> UserExistsAndActiveAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task<IReadOnlyList<Guid>> GetDepartmentManagersAsync(Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(new List<Guid>());
        public Task<StoredRequestRouting?> FindReplayAsync(CancellationToken cancellationToken) =>
            Task.FromResult<StoredRequestRouting?>(null);
        public Task CommitAsync(RequestRouting routing, AuditLog audit, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeReceiptStore(WorkRequest request, RequestRouting routing, bool isUserInDept) : IRequestReceiptStore, IRequestReceiptTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => routing;
        public Task<IRequestReceiptTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken) =>
            Task.FromResult<IRequestReceiptTransaction>(this);
        public Task<bool> IsUserInDepartmentAsync(Guid userId, Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(isUserInDept);
        public Task<StoredRequestReceipt?> FindReplayAsync(CancellationToken cancellationToken) =>
            Task.FromResult<StoredRequestReceipt?>(null);
        public Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRejectionStore(WorkRequest request) : IRequestRejectionStore, IRequestRejectionTransaction
    {
        public WorkRequest? Request => request;
        public Task<IRequestRejectionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken) =>
            Task.FromResult<IRequestRejectionTransaction>(this);
        public Task<StoredRequestRejection?> FindReplayAsync(CancellationToken cancellationToken) =>
            Task.FromResult<StoredRequestRejection?>(null);
        public Task CommitAsync(AuditLog audit, Notification notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
