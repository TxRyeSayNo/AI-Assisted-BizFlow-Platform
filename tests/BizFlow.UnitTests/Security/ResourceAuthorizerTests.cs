using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

public sealed class ResourceAuthorizerTests
{
    [Fact]
    public async Task Denial_is_audited_in_callers_scope_and_conceals_foreign_resource()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var foreignTenant = Guid.NewGuid();
        var provider = new Provider(Snapshot(context));
        var audit = new Audit();
        var authorizer = new ResourceAuthorizer(context, provider, audit);

        var exception = await Assert.ThrowsAsync<ApplicationFault>(() =>
            authorizer.AuthorizeAsync("users.update", new(foreignTenant)));

        Assert.Equal(FaultKind.NotFound, exception.Kind);
        Assert.Equal("RESOURCE.NOT_FOUND", exception.Code);
        Assert.Equal(context.TenantId, audit.TenantId);
        Assert.Equal(context.UserId, audit.UserId);
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task Current_account_activity_is_resolved_on_every_operation()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var provider = new Provider(Snapshot(context));
        var audit = new Audit();
        var authorizer = new ResourceAuthorizer(context, provider, audit);
        await authorizer.AuthorizeAsync("users.update", new(context.TenantId));
        provider.Value = provider.Value! with { IsUserActive = false };
        await Assert.ThrowsAsync<ApplicationFault>(() => authorizer.AuthorizeAsync("users.update", new(context.TenantId)));
        Assert.Equal(2, provider.Calls);
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task Resolver_identity_mismatch_is_rejected()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var authorizer = new ResourceAuthorizer(context,
            new Provider(Snapshot(context) with { UserId = Guid.NewGuid() }), new Audit());
        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => authorizer.AuthorizeAsync("users.update", new(context.TenantId)));
        Assert.Equal(FaultKind.Unauthenticated, fault.Kind);
    }

    private static AccessSnapshot Snapshot(Context context) => new(context.UserId!.Value, context.TenantId,
        true, true, [new("users.update", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());

    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;

    private sealed class Provider(AccessSnapshot? snapshot) : IAccessSnapshotProvider
    {
        public AccessSnapshot? Value { get; set; } = snapshot;
        public int Calls { get; private set; }
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Value);
        }
    }

    private sealed class Audit : ISecurityAuditWriter
    {
        public int Count { get; private set; }
        public Guid? TenantId { get; private set; }
        public Guid? UserId { get; private set; }
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken)
        {
            Count++;
            TenantId = tenantId;
            UserId = userId;
            return Task.CompletedTask;
        }
    }
}
