using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

public sealed class TenantMembershipTests
{
    [Fact]
    public async Task Authenticated_catalog_read_does_not_invent_a_role_requirement()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var audit = new Audit();
        var membership = new TenantMembershipAuthorizer(context, new Snapshot(new(context.UserId!.Value,
            context.TenantId, true, true, [], new HashSet<Guid>(), new HashSet<Guid>())), audit);
        Assert.Equal(context.TenantId, await membership.RequireAsync("departments.read", default));
        Assert.False(audit.Called);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Inactive_context_is_denied_and_audited(bool userActive, bool tenantActive)
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var audit = new Audit();
        var membership = new TenantMembershipAuthorizer(context, new Snapshot(new(context.UserId!.Value,
            context.TenantId, userActive, tenantActive, [], new HashSet<Guid>(), new HashSet<Guid>())), audit);
        var error = await Assert.ThrowsAsync<ApplicationFault>(() => membership.RequireAsync("departments.read", default));
        Assert.Equal(FaultKind.Forbidden, error.Kind);
        Assert.True(audit.Called);
    }

    [Fact]
    public async Task Tenantless_platform_identity_cannot_read_a_tenant_catalog()
    {
        var audit = new Audit();
        var membership = new TenantMembershipAuthorizer(new Context(Guid.NewGuid(), null), new Snapshot(null), audit);
        var error = await Assert.ThrowsAsync<ApplicationFault>(() => membership.RequireAsync("departments.read", default));
        Assert.Equal(FaultKind.Forbidden, error.Kind);
        Assert.True(audit.Called);
    }

    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;
    private sealed class Snapshot(AccessSnapshot? value) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) => Task.FromResult(value);
    }
    private sealed class Audit : ISecurityAuditWriter
    {
        public bool Called { get; private set; }
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken)
        { Called = true; return Task.CompletedTask; }
    }
}
