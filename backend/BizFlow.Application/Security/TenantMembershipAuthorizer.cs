using BizFlow.Application.Common;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Security;

// Active membership for canonical "Authenticated" operations (API-ORG-05, API-NOTIF-01).
// Recipient-only notification receipts additionally enforce ownership in Domain/Application/storage.
// Permission-gated business mutations and reads continue to use IResourceAuthorizer.
public sealed class TenantMembershipAuthorizer(ITenantContext context, IAccessSnapshotProvider snapshots, ISecurityAuditWriter audit)
{
    public async Task<Guid> RequireAsync(string operation, CancellationToken cancellationToken)
    {
        var reason = AccessDenial.None;
        if (context.UserId is not { } userId || userId == Guid.Empty) reason = AccessDenial.Unauthenticated;
        else if (context.TenantId is not { } tenantId || tenantId == Guid.Empty) reason = AccessDenial.PermissionDenied;
        else
        {
            var access = await snapshots.ResolveAsync(userId, tenantId, cancellationToken);
            reason = access is null || access.UserId != userId || access.TenantId != tenantId ? AccessDenial.Unauthenticated :
                !access.IsUserActive ? AccessDenial.InactiveAccount : !access.IsTenantActive ? AccessDenial.InactiveTenant : AccessDenial.None;
            if (reason == AccessDenial.None) return tenantId;
        }
        await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, operation, reason, cancellationToken);
        throw reason == AccessDenial.Unauthenticated
            ? new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.")
            : new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "An active tenant account is required.");
    }
}
