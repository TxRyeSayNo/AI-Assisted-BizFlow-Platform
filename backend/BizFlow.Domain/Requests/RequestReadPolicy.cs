using BizFlow.Domain.Security;

namespace BizFlow.Domain.Requests;

public sealed record RequestReadScope(
    Guid TenantId,
    Guid UserId,
    bool Tenant,
    bool Managed,
    bool Own,
    IReadOnlySet<Guid> ManagedDepartmentIds);

public static class RequestReadPolicy
{
    public const string TenantPermission = "requests.read.tenant";
    public const string ManagedPermission = "requests.read.managed";
    public const string OwnPermission = "requests.read.own";

    public static (AccessDecision Decision, RequestReadScope? Scope) Resolve(AccessSnapshot access)
    {
        if (access.UserId == Guid.Empty) return Deny(AccessDenial.Unauthenticated);
        if (!access.IsUserActive) return Deny(AccessDenial.InactiveAccount);
        if (access.TenantId is not { } tenant || tenant == Guid.Empty) return Deny(AccessDenial.PermissionDenied);
        if (!access.IsTenantActive) return Deny(AccessDenial.InactiveTenant);

        bool Has(string code, PermissionScope scope) => access.Grants.Contains(new(code, scope));
        var all = Has(TenantPermission, PermissionScope.Tenant);
        var managed = Has(ManagedPermission, PermissionScope.Department);
        var own = Has(OwnPermission, PermissionScope.Self);

        if (!all && !managed && !own) return Deny(AccessDenial.PermissionDenied);
        return (AccessDecision.Permit, new(tenant, access.UserId, all, managed, own,
            access.ManagedDepartmentIds.ToHashSet()));
    }

    private static (AccessDecision, RequestReadScope?) Deny(AccessDenial reason) => (AccessDecision.Deny(reason), null);
}
