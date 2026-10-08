using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using Xunit;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestResolutionAndConfirmationCommandTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _resolverId = Guid.NewGuid();
    private readonly Guid _departmentId = Guid.NewGuid();

    [Fact]
    public void RequestResolution_Audit_And_Confirmation_Factories_Validate_State()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _requesterId, RequestState.InProgress, now);
        request.ApplyResolutionTransition(new(_resolverId, RequestState.InProgress, RequestState.Resolved, request.UpdatedAt), now);

        var resolution = RequestResolution.Create(request.Id, _resolverId, "Resolution details", 1, now);
        var resAudit = AuditLog.RequestResolved(request, resolution, now);
        Assert.Equal("REQUEST.RESOLVED", resAudit.Action);
        Assert.Equal(_resolverId, resAudit.ActorId);

        // Confirmation (accept)
        request.ResetWorkflowMutation();
        request.ApplyConfirmationTransition(new(_requesterId, RequestState.Resolved, RequestState.Confirmed, request.UpdatedAt), now);
        var conf = Confirmation.RequestResolutionConfirmed(request, _requesterId, "Looks good");
        var confAudit = AuditLog.RequestConfirmed(request, conf, now);
        Assert.Equal("REQUEST.CONFIRMED", confAudit.Action);
        Assert.Equal("CONFIRMED", conf.Decision);
        Assert.Equal("RESOLUTION", conf.MilestoneType);

        // Closure
        request.ResetWorkflowMutation();
        request.ApplyClosureTransition(new(_requesterId, RequestState.Confirmed, RequestState.Closed, request.UpdatedAt), now);
        var closeAudit = AuditLog.RequestClosed(request, now);
        Assert.Equal("REQUEST.CLOSED", closeAudit.Action);

        // Rework (reject)
        var reworkReq = RequestInState(_tenantId, _requesterId, RequestState.Resolved, now);
        reworkReq.ApplyConfirmationTransition(new(_requesterId, RequestState.Resolved, RequestState.InProgress, reworkReq.UpdatedAt), now);
        var reworkConf = Confirmation.RequestResolutionRejected(reworkReq, _requesterId, "Fix section B");
        var reworkAudit = AuditLog.RequestRework(reworkReq, reworkConf, now);
        Assert.Equal("REQUEST.REWORK", reworkAudit.Action);
        Assert.Equal("REJECTED", reworkConf.Decision);

        // Revision audit
        var rejectedReq = RequestInState(_tenantId, _requesterId, RequestState.Submitted, now);
        rejectedReq.ApplyRejectionTransition(new(_resolverId, RequestState.Submitted, RequestState.Rejected, rejectedReq.UpdatedAt), now);
        var revision = WorkRequest.ReviseRejected(rejectedReq, _requesterId, rejectedReq.ServiceId, rejectedReq.CategoryId, "Revised title", "Revised desc", now);
        var revAudit = AuditLog.RequestRevised(rejectedReq, revision, now);
        Assert.Equal("REQUEST.REVISED", revAudit.Action);
    }

    [Fact]
    public async Task RequestResolutionCommand_Validates_Content_And_State()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _requesterId, RequestState.InProgress, now);
        var store = new FakeResolutionStore(request, 1);
        var command = new RequestResolutionCommand(
            new TestContext(_tenantId, _resolverId),
            new TestAuthorizer(true),
            new TestSnapshotProvider(new(_resolverId, _tenantId, true, true,
                [new("requests.resolve", PermissionScope.Tenant)], new HashSet<Guid> { _departmentId }, new HashSet<Guid>())),
            new TestAuditWriter(),
            store,
            TimeProvider.System,
            new TestNotificationUpdates());

        // Empty content throws validation fault
        await Assert.ThrowsAsync<ApplicationFault>(() =>
            command.ResolveAsync(request.Id, new("   "), null, CancellationToken.None));

        // Valid resolve transitions to Resolved
        var result = await command.ResolveAsync(request.Id, new("Fix applied successfully"), null, CancellationToken.None);
        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(1, result.RevisionNo);
        Assert.Equal("Fix applied successfully", result.Content);
        Assert.Equal(RequestState.Resolved, request.Status);
    }

    [Fact]
    public async Task RequestConfirmationCommand_Confirms_And_Closes_Request()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _requesterId, RequestState.Resolved, now);
        var store = new FakeConfirmationStore(request);
        var command = new RequestConfirmationCommand(
            new TestContext(_tenantId, _requesterId),
            new TestAuthorizer(true),
            new TestSnapshotProvider(new(_requesterId, _tenantId, true, true,
                [new("requests.confirm", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>())),
            new TestAuditWriter(),
            store,
            TimeProvider.System,
            new TestNotificationUpdates());

        // Confirm
        var result = await command.ConfirmAsync(request.Id, new("CONFIRMED", "All good"), null, CancellationToken.None);
        Assert.Equal("CONFIRMED", result.Decision);
        Assert.Equal("CLOSED", result.Status);
        Assert.Equal(RequestState.Closed, request.Status);
        Assert.NotNull(request.ClosedAt);
    }

    [Fact]
    public async Task RequestConfirmationCommand_Rework_Transitions_To_InProgress()
    {
        var now = DateTimeOffset.UtcNow;
        var request = RequestInState(_tenantId, _requesterId, RequestState.Resolved, now);
        var store = new FakeConfirmationStore(request);
        var command = new RequestConfirmationCommand(
            new TestContext(_tenantId, _requesterId),
            new TestAuthorizer(true),
            new TestSnapshotProvider(new(_requesterId, _tenantId, true, true,
                [new("requests.confirm", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>())),
            new TestAuditWriter(),
            store,
            TimeProvider.System,
            new TestNotificationUpdates());

        // Rework without reason throws validation fault
        await Assert.ThrowsAsync<ApplicationFault>(() =>
            command.ConfirmAsync(request.Id, new("REWORK", null), null, CancellationToken.None));

        // Rework with reason transitions back to InProgress
        var result = await command.ConfirmAsync(request.Id, new("REWORK", "Missing attachment"), null, CancellationToken.None);
        Assert.Equal("REWORK", result.Decision);
        Assert.Equal("IN_PROGRESS", result.Status);
        Assert.Equal(RequestState.InProgress, request.Status);
    }

    [Fact]
    public async Task RequestReviseCommand_Creates_New_Draft_From_Rejected()
    {
        var now = DateTimeOffset.UtcNow;
        var rejectedReq = RequestInState(_tenantId, _requesterId, RequestState.Submitted, now);
        rejectedReq.ApplyRejectionTransition(new(_resolverId, RequestState.Submitted, RequestState.Rejected, rejectedReq.UpdatedAt), now);

        var store = new FakeReviseStore(rejectedReq);
        var command = new RequestReviseCommand(
            new TestContext(_tenantId, _requesterId),
            new TestAuthorizer(true),
            new TestSnapshotProvider(new(_requesterId, _tenantId, true, true,
                [new("requests.create", PermissionScope.Tenant), new("requests.submit", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>())),
            new TestAuditWriter(),
            store,
            TimeProvider.System);

        var result = await command.ReviseAsync(rejectedReq.Id, new("Updated title", "Updated desc", "High", SubmitImmediately: true), null, CancellationToken.None);
        Assert.NotEqual(rejectedReq.Id, result.RequestId);
        Assert.Equal(rejectedReq.Id, result.SourceRequestId);
        Assert.Equal("Updated title", result.Title);
        Assert.Equal("SUBMITTED", result.Status);
    }

    private static WorkRequest RequestInState(Guid tenantId, Guid requesterId, RequestState targetState, DateTimeOffset now)
    {
        var req = WorkRequest.CreateSubmitted(tenantId, requesterId, Guid.NewGuid(), Guid.NewGuid(), "Test title", "Test description", now);
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(req, targetState);
        return req;
    }

    private sealed class FakeResolutionStore(WorkRequest request, int nextRev = 1) : IRequestResolutionStore, IRequestResolutionTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => null;
        public UserAccount? Actor => null;
        public Task<int> NextRevisionNoAsync(CancellationToken cancellationToken) => Task.FromResult(nextRev);
        public Task<StoredRequestResolution?> FindReplayAsync(CancellationToken cancellationToken) => Task.FromResult<StoredRequestResolution?>(null);
        public Task CommitAsync(RequestResolution resolution, Action applyTransition, Func<AuditLog> auditFactory, Notification notification, CancellationToken cancellationToken)
        {
            applyTransition();
            return Task.CompletedTask;
        }
        public Task<IRequestResolutionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken) =>
            Task.FromResult<IRequestResolutionTransaction>(this);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeConfirmationStore(WorkRequest request) : IRequestConfirmationStore, IRequestConfirmationTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => null;
        public UserAccount? Actor => null;
        public Task<StoredRequestConfirmation?> FindReplayAsync(CancellationToken cancellationToken) => Task.FromResult<StoredRequestConfirmation?>(null);
        public Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, Action? applyClosure, Func<AuditLog>? closeAuditFactory, CancellationToken cancellationToken)
        {
            applyClosure?.Invoke();
            return Task.CompletedTask;
        }
        public Task<IRequestConfirmationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken) =>
            Task.FromResult<IRequestConfirmationTransaction>(this);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeReviseStore(WorkRequest source) : IRequestReviseStore, IRequestReviseTransaction
    {
        public WorkRequest? SourceRequest => source;
        public UserAccount? Actor => null;
        public Task<StoredRequestRevision?> FindReplayAsync(CancellationToken cancellationToken) => Task.FromResult<StoredRequestRevision?>(null);
        public Task CommitAsync(WorkRequest revision, AuditLog audit, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IRequestReviseTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid sourceRequestId, string? keyHash, CancellationToken cancellationToken) =>
            Task.FromResult<IRequestReviseTransaction>(this);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
    }

    private sealed class TestAuthorizer(bool allow) : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? targetDept = null, CancellationToken cancellationToken = default) =>
            allow ? Task.CompletedTask : throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Denied");
    }

    private sealed class TestSnapshotProvider(AccessSnapshot snapshot) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccessSnapshot?>(snapshot);
    }

    private sealed class TestAuditWriter : ISecurityAuditWriter
    {
        public Task RecordDeniedAccessAsync(Guid? actorId, Guid? tenantId, string permission, AccessDenial denial, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestNotificationUpdates : INotificationUpdates
    {
        public Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientUserIds, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
