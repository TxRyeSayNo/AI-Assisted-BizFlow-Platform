using BizFlow.Domain.Security;

namespace BizFlow.Domain.Tasks;

// ADR-0011. These are capabilities, not role names. The resulting scope is server-only.
public sealed record TaskReadScope(Guid TenantId, Guid UserId, bool Tenant, bool Managed, bool Own,
    bool Assigned, IReadOnlySet<Guid> ManagedDepartmentIds);

public static class TaskReadPolicy
{
    public const string TenantPermission = "tasks.read.tenant";
    public const string ManagedPermission = "tasks.read.managed";
    public const string OwnPermission = "tasks.read.own";
    public const string AssignedPermission = "tasks.read.assigned";

    public static (AccessDecision Decision, TaskReadScope? Scope) Resolve(AccessSnapshot access)
    {
        if (access.UserId == Guid.Empty) return Deny(AccessDenial.Unauthenticated);
        if (!access.IsUserActive) return Deny(AccessDenial.InactiveAccount);
        if (access.TenantId is not { } tenant || tenant == Guid.Empty) return Deny(AccessDenial.PermissionDenied);
        if (!access.IsTenantActive) return Deny(AccessDenial.InactiveTenant);
        bool Has(string code, PermissionScope scope) => access.Grants.Contains(new(code, scope));
        var all = Has(TenantPermission, PermissionScope.Tenant);
        var managed = Has(ManagedPermission, PermissionScope.Department);
        var own = Has(OwnPermission, PermissionScope.Self);
        var assigned = Has(AssignedPermission, PermissionScope.Assigned);
        if (!all && !managed && !own && !assigned) return Deny(AccessDenial.PermissionDenied);
        return (AccessDecision.Permit, new(tenant, access.UserId, all, managed, own, assigned,
            access.ManagedDepartmentIds.ToHashSet()));
    }

    private static (AccessDecision, TaskReadScope?) Deny(AccessDenial reason) => (AccessDecision.Deny(reason), null);
}
