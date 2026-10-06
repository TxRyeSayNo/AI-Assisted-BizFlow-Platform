using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Notifications;

public sealed class NotificationAuthorizationTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Inactive_membership_before_or_after_lock_prevents_receipt_and_commit(int deniedCall, bool tenantInactive)
    {
        var context = new Context(); var snapshots = new Snapshots(deniedCall, tenantInactive);
        var audit = new Audit(); var store = new Store();
        var membership = new TenantMembershipAuthorizer(context, snapshots, audit);
        var service = new NotificationInbox(context, membership, audit, store, TimeProvider.System);

        var error = await Assert.ThrowsAsync<ApplicationFault>(() => service.MarkReadAsync(store.Transaction.Notification.Id, default));

        Assert.Equal(FaultKind.Forbidden, error.Kind);
        Assert.Equal(deniedCall, snapshots.Calls);
        Assert.Equal(deniedCall == 2, store.Opened);
        Assert.Equal(deniedCall == 2, store.Transaction.Disposed);
        Assert.False(store.Transaction.Committed);
        Assert.Null(store.Transaction.Notification.ReadAt);
        Assert.Equal(tenantInactive ? AccessDenial.InactiveTenant : AccessDenial.InactiveAccount, audit.Denial);
    }

    private sealed class Context : ITenantContext { public Guid? UserId => User; public Guid? TenantId => Tenant; }
    private sealed class Snapshots(int deniedCall, bool tenantInactive) : IAccessSnapshotProvider
    {
        public int Calls { get; private set; }
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
        {
            Assert.Equal(User, userId); Assert.Equal(Tenant, tenantId);
            var denied = ++Calls == deniedCall;
            return Task.FromResult<AccessSnapshot?>(new(User, Tenant, !(denied && !tenantInactive),
                !(denied && tenantInactive), [], new HashSet<Guid>(), new HashSet<Guid>()));
        }
    }
    private sealed class Audit : ISecurityAuditWriter
    {
        public AccessDenial? Denial { get; private set; }
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken)
        {
            Assert.Equal(User, userId); Assert.Equal(Tenant, tenantId); Assert.Equal("notifications.mark-read", permission);
            Denial = reason; return Task.CompletedTask;
        }
    }
    private sealed class Store : INotificationInboxStore
    {
        public Transaction Transaction { get; } = new();
        public bool Opened { get; private set; }
        public Task<INotificationReceiptTransaction?> BeginReadAsync(Guid tenantId, Guid recipientId, Guid id, CancellationToken cancellationToken)
        {
            Assert.Equal(Tenant, tenantId); Assert.Equal(User, recipientId); Assert.Equal(Transaction.Notification.Id, id);
            Opened = true; return Task.FromResult<INotificationReceiptTransaction?>(Transaction);
        }
        public Task<NotificationPage> ListAsync(Guid tenantId, Guid recipientId, NotificationFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Transaction : INotificationReceiptTransaction
    {
        public Notification Notification { get; } = Notification.Create(Tenant, User, NotificationEvent.TaskAssigned, "Assigned", "Work update", "event/recipient");
        public bool Disposed { get; private set; }
        public bool Committed { get; private set; }
        public Task<NotificationRow> CommitAsync(AuditLog? audit, CancellationToken cancellationToken)
        { Committed = true; throw new InvalidOperationException("A denied receipt must not commit."); }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
