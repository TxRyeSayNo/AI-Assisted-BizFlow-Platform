namespace BizFlow.Domain.Security;

// The catalog is seeded through migrations; it is not a tenant-editable authority source.
public sealed class Permission
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Module { get; private set; } = "";
    public string Action { get; private set; } = "";
    public PermissionScope ScopeType { get; private set; }
    private Permission() { }
}

public sealed class RolePermission(Guid roleId, Guid permissionId)
{
    public Guid RoleId { get; private set; } = roleId;
    public Guid PermissionId { get; private set; } = permissionId;
}

public sealed class UserRole(Guid userId, Guid roleId)
{
    public Guid UserId { get; private set; } = userId;
    public Guid RoleId { get; private set; } = roleId;
}
