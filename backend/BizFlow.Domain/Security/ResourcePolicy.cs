namespace BizFlow.Domain.Security;

public enum PermissionScope { Platform, Tenant, Department, Self, Assigned }
public enum AccessDenial { None, Unauthenticated, InactiveAccount, InactiveTenant, ResourceNotFound, PermissionDenied, ManagementScopeDenied }

public sealed record PermissionGrant(string Code, PermissionScope Scope);

// A current server-resolved snapshot, never data supplied by a request body or AI output.
// Department sets are already expanded through authorized descendant scopes by the resolver.
public sealed record AccessSnapshot(
    Guid UserId,
    Guid? TenantId,
    bool IsUserActive,
    bool IsTenantActive,
    IReadOnlyCollection<PermissionGrant> Grants,
    IReadOnlySet<Guid> DepartmentIds,
    IReadOnlySet<Guid> ManagedDepartmentIds);

public sealed record ResourceScope(
    Guid? TenantId,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    IReadOnlyCollection<Guid>? AssignedUserIds = null);

public readonly record struct AccessDecision(bool Allowed, AccessDenial Denial)
{
    public static AccessDecision Permit => new(true, AccessDenial.None);
    public static AccessDecision Deny(AccessDenial denial) => new(false, denial);
}

/// <summary>BR-001/002/003/006. Actors and role names never substitute for grants.</summary>
public static class ResourcePolicy
{
    public static AccessDecision Evaluate(AccessSnapshot access, string permission,
        ResourceScope resource, Guid? managementTargetDepartmentId = null)
    {
        if (access.UserId == Guid.Empty)
            return AccessDecision.Deny(AccessDenial.Unauthenticated);
        if (!access.IsUserActive)
            return AccessDecision.Deny(AccessDenial.InactiveAccount);

        if (resource.TenantId is { } tenant)
        {
            if (tenant == Guid.Empty || access.TenantId != tenant)
                return AccessDecision.Deny(AccessDenial.ResourceNotFound);
            if (!access.IsTenantActive)
                return AccessDecision.Deny(AccessDenial.InactiveTenant);
        }

        var allowed = access.Grants.Any(grant =>
            string.Equals(grant.Code, permission, StringComparison.Ordinal) &&
            grant.Scope switch
            {
                PermissionScope.Platform => resource.TenantId is null,
                PermissionScope.Tenant => resource.TenantId is not null,
                PermissionScope.Department => resource.TenantId is not null &&
                    resource.DepartmentId is { } department && access.DepartmentIds.Contains(department),
                PermissionScope.Self => resource.TenantId is not null && resource.OwnerId == access.UserId,
                PermissionScope.Assigned => resource.TenantId is not null &&
                    resource.AssignedUserIds?.Contains(access.UserId) == true,
                _ => false
            });

        if (!allowed)
            return AccessDecision.Deny(AccessDenial.PermissionDenied);
        if (managementTargetDepartmentId is { } target &&
            (target == Guid.Empty || !access.ManagedDepartmentIds.Contains(target)))
            return AccessDecision.Deny(AccessDenial.ManagementScopeDenied);
        return AccessDecision.Permit;
    }
}
