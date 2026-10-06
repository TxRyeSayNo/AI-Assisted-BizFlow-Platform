using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskReadPolicyTests
{
    [Theory]
    [InlineData("tasks.read.tenant", PermissionScope.Tenant)]
    [InlineData("tasks.read.managed", PermissionScope.Department)]
    [InlineData("tasks.read.own", PermissionScope.Self)]
    [InlineData("tasks.read.assigned", PermissionScope.Assigned)]
    public void Only_exact_catalog_capabilities_are_accepted(string permission, PermissionScope required)
    {
        foreach (var scope in Enum.GetValues<PermissionScope>())
        {
            var result = TaskReadPolicy.Resolve(Snapshot() with { Grants = [new(permission, scope)] });
            Assert.Equal(scope == required, result.Decision.Allowed);
            Assert.Equal(scope == required, result.Scope is not null);
        }
    }

    [Fact]
    public void Union_preserves_all_four_grants_and_copies_server_resolved_scopes()
    {
        var department = Guid.NewGuid(); var managed = Guid.NewGuid();
        var source = Snapshot() with
        {
            Grants = [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant), new(TaskReadPolicy.ManagedPermission, PermissionScope.Department),
                new(TaskReadPolicy.OwnPermission, PermissionScope.Self), new(TaskReadPolicy.AssignedPermission, PermissionScope.Assigned)],
            DepartmentIds = new HashSet<Guid> { department }, ManagedDepartmentIds = new HashSet<Guid> { managed }
        };
        var result = TaskReadPolicy.Resolve(source);
        Assert.True(result.Decision.Allowed); var scope = Assert.IsType<TaskReadScope>(result.Scope);
        Assert.True(scope.Tenant && scope.Managed && scope.Own && scope.Assigned);
        Assert.Equal(managed, Assert.Single(scope.ManagedDepartmentIds));
        Assert.NotSame(source.ManagedDepartmentIds, scope.ManagedDepartmentIds);
    }

    [Fact]
    public void Inactive_tenantless_empty_identity_and_missing_grants_fail_closed()
    {
        var valid = Snapshot();
        foreach (var invalid in new[] { valid with { UserId = Guid.Empty }, valid with { IsUserActive = false },
            valid with { IsTenantActive = false }, valid with { TenantId = null }, valid with { TenantId = Guid.Empty },
            valid with { Grants = [] }, valid with { Grants = [new("COMPANY_ADMIN", PermissionScope.Tenant)] } })
        {
            var result = TaskReadPolicy.Resolve(invalid);
            Assert.False(result.Decision.Allowed); Assert.Null(result.Scope);
        }
    }
    private static AccessSnapshot Snapshot() => new(Guid.NewGuid(), Guid.NewGuid(), true, true,
        [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
}
