using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

public sealed class RoleConfigurationTests
{
    [Fact]
    public void Custom_role_accepts_only_non_platform_permissions_and_updates_modification_time()
    {
        var now = DateTimeOffset.UtcNow;
        var role = Role.CreateCustom(Guid.NewGuid(), "Support", now);
        role.ConfigurePermissions([PermissionScope.Tenant, PermissionScope.Self, PermissionScope.Department, PermissionScope.Assigned], now.AddMinutes(1));
        Assert.Equal(now.AddMinutes(1), role.UpdatedAt);
        Assert.Throws<InvalidOperationException>(() => role.ConfigurePermissions([PermissionScope.Platform], now.AddMinutes(2)));
        Assert.Equal(now.AddMinutes(1), role.UpdatedAt);
    }

    [Fact]
    public void Empty_custom_role_is_a_valid_non_authoritative_configuration()
    {
        var now = DateTimeOffset.UtcNow;
        var role = Role.CreateCustom(Guid.NewGuid(), "No access", now);
        role.ConfigurePermissions([], now.AddMinutes(1));
        Assert.False(role.IsSystem);
    }
}
