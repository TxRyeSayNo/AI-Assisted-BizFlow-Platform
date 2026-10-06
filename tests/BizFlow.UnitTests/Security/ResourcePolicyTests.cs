using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

// BR-001/002/003/006: the same policy applies to human and AI callers.
public sealed class ResourcePolicyTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Department = Guid.NewGuid();
    private static readonly ResourceScope Resource = new(TenantA, User, Department, [User]);

    private static AccessSnapshot Access(PermissionScope scope, Guid? tenant = null) =>
        new(User, tenant ?? TenantA, true, true,
            [new("tasks.assign", scope)], new HashSet<Guid> { Department }, new HashSet<Guid> { Department });

    [Theory]
    [InlineData(PermissionScope.Tenant)]
    [InlineData(PermissionScope.Department)]
    [InlineData(PermissionScope.Self)]
    [InlineData(PermissionScope.Assigned)]
    [InlineData(PermissionScope.Platform)]
    public void Every_permission_scope_denies_cross_tenant_resources(PermissionScope scope)
    {
        var decision = ResourcePolicy.Evaluate(Access(scope), "tasks.assign", Resource with { TenantId = TenantB });
        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenial.ResourceNotFound, decision.Denial);
    }

    [Fact]
    public void Platform_grant_does_not_implicitly_grant_tenant_business_access()
    {
        Assert.False(ResourcePolicy.Evaluate(Access(PermissionScope.Platform), "tasks.assign", Resource).Allowed);
    }

    [Fact]
    public void Platform_resource_requires_explicit_platform_grant()
    {
        var platform = new ResourceScope(null);
        Assert.True(ResourcePolicy.Evaluate(Access(PermissionScope.Platform), "tasks.assign", platform).Allowed);
        Assert.False(ResourcePolicy.Evaluate(Access(PermissionScope.Tenant), "tasks.assign", platform).Allowed);
    }

    [Theory]
    [InlineData(PermissionScope.Tenant)]
    [InlineData(PermissionScope.Department)]
    [InlineData(PermissionScope.Self)]
    [InlineData(PermissionScope.Assigned)]
    public void Matching_grant_and_resource_scope_allow_access(PermissionScope scope)
    {
        Assert.True(ResourcePolicy.Evaluate(Access(scope), "tasks.assign", Resource).Allowed);
    }

    [Fact]
    public void Self_requires_ownership_and_assigned_requires_assignment()
    {
        Assert.False(ResourcePolicy.Evaluate(Access(PermissionScope.Self), "tasks.assign", Resource with { OwnerId = Guid.NewGuid() }).Allowed);
        Assert.False(ResourcePolicy.Evaluate(Access(PermissionScope.Assigned), "tasks.assign", Resource with { AssignedUserIds = [] }).Allowed);
    }

    [Fact]
    public void Department_scope_requires_server_resolved_membership()
    {
        Assert.False(ResourcePolicy.Evaluate(Access(PermissionScope.Department), "tasks.assign", Resource with { DepartmentId = Guid.NewGuid() }).Allowed);
    }

    [Fact]
    public void Manager_target_scope_is_an_additional_guard_even_with_tenant_permission()
    {
        var access = Access(PermissionScope.Tenant);
        Assert.True(ResourcePolicy.Evaluate(access, "tasks.assign", Resource, Department).Allowed);
        Assert.False(ResourcePolicy.Evaluate(access, "tasks.assign", Resource, Guid.NewGuid()).Allowed);
    }

    [Fact]
    public void Inactive_user_or_tenant_and_missing_identity_fail_closed()
    {
        var access = Access(PermissionScope.Tenant);
        Assert.False(ResourcePolicy.Evaluate(access with { IsUserActive = false }, "tasks.assign", Resource).Allowed);
        Assert.False(ResourcePolicy.Evaluate(access with { IsTenantActive = false }, "tasks.assign", Resource).Allowed);
        Assert.False(ResourcePolicy.Evaluate(access with { UserId = Guid.Empty }, "tasks.assign", Resource).Allowed);
        Assert.False(ResourcePolicy.Evaluate(access with { TenantId = null }, "tasks.assign", Resource).Allowed);
    }

    [Fact]
    public void Permission_codes_are_exact_and_no_wildcards_or_role_names_grant_access()
    {
        var access = Access(PermissionScope.Tenant);
        Assert.False(ResourcePolicy.Evaluate(access, "tasks.cancel", Resource).Allowed);
        Assert.False(ResourcePolicy.Evaluate(access, "TASKS.ASSIGN", Resource).Allowed);
        Assert.False(ResourcePolicy.Evaluate(access with { Grants = [new("MANAGER", PermissionScope.Tenant), new("*", PermissionScope.Tenant)] }, "tasks.assign", Resource).Allowed);
    }
}
