using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestReadPolicyTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Dept1 = Guid.NewGuid();

    [Fact]
    public void Unauthenticated_user_is_denied()
    {
        var snapshot = new AccessSnapshot(Guid.Empty, Tenant, true, true, [], new HashSet<Guid>(), new HashSet<Guid>());
        var (decision, scope) = RequestReadPolicy.Resolve(snapshot);
        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenial.Unauthenticated, decision.Denial);
        Assert.Null(scope);
    }

    [Fact]
    public void Inactive_user_is_denied()
    {
        var snapshot = new AccessSnapshot(User, Tenant, false, true, [new(RequestReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        var (decision, scope) = RequestReadPolicy.Resolve(snapshot);
        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenial.InactiveAccount, decision.Denial);
        Assert.Null(scope);
    }

    [Fact]
    public void Missing_grants_is_denied()
    {
        var snapshot = new AccessSnapshot(User, Tenant, true, true, [], new HashSet<Guid>(), new HashSet<Guid>());
        var (decision, scope) = RequestReadPolicy.Resolve(snapshot);
        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenial.PermissionDenied, decision.Denial);
        Assert.Null(scope);
    }

    [Fact]
    public void Tenant_grant_resolves_tenant_scope()
    {
        var snapshot = new AccessSnapshot(User, Tenant, true, true, [new(RequestReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        var (decision, scope) = RequestReadPolicy.Resolve(snapshot);
        Assert.True(decision.Allowed);
        Assert.NotNull(scope);
        Assert.True(scope.Tenant);
        Assert.False(scope.Managed);
        Assert.False(scope.Own);
    }

    [Fact]
    public void Managed_grant_resolves_managed_scope_with_departments()
    {
        var snapshot = new AccessSnapshot(User, Tenant, true, true, [new(RequestReadPolicy.ManagedPermission, PermissionScope.Department)], new HashSet<Guid> { Dept1 }, new HashSet<Guid> { Dept1 });
        var (decision, scope) = RequestReadPolicy.Resolve(snapshot);
        Assert.True(decision.Allowed);
        Assert.NotNull(scope);
        Assert.False(scope.Tenant);
        Assert.True(scope.Managed);
        Assert.False(scope.Own);
        Assert.Contains(Dept1, scope.ManagedDepartmentIds);
    }

    [Fact]
    public void Own_grant_resolves_own_scope()
    {
        var snapshot = new AccessSnapshot(User, Tenant, true, true, [new(RequestReadPolicy.OwnPermission, PermissionScope.Self)], new HashSet<Guid>(), new HashSet<Guid>());
        var (decision, scope) = RequestReadPolicy.Resolve(snapshot);
        Assert.True(decision.Allowed);
        Assert.NotNull(scope);
        Assert.False(scope.Tenant);
        Assert.False(scope.Managed);
        Assert.True(scope.Own);
    }
}
