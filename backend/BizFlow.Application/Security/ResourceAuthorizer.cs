using BizFlow.Application.Common;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Security;

public sealed class ResourceAuthorizer(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit) : IResourceAuthorizer
{
    public async Task AuthorizeAsync(string permission, ResourceScope resource,
        Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
    {
        var snapshot = context.UserId is { } userId && userId != Guid.Empty
            ? await snapshots.ResolveAsync(userId, context.TenantId, cancellationToken)
            : null;
        var decision = snapshot is null || snapshot.UserId != context.UserId || snapshot.TenantId != context.TenantId
            ? AccessDecision.Deny(AccessDenial.Unauthenticated)
            : ResourcePolicy.Evaluate(snapshot, permission, resource, managementTargetDepartmentId);

        if (decision.Allowed) return;

        // Do not record another tenant's resource data in the caller's audit scope.
        await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, permission, decision.Denial, cancellationToken);
        throw decision.Denial switch
        {
            AccessDenial.Unauthenticated => new ApplicationFault(FaultKind.Unauthenticated,
                "AUTH.REQUIRED", "Authentication is required."),
            AccessDenial.ResourceNotFound => ApplicationFault.NotFound(),
            AccessDenial.ManagementScopeDenied => new ApplicationFault(FaultKind.Forbidden,
                "TASK.TARGET_OUT_OF_SCOPE", "The assignment target is outside your configured management scope."),
            _ => new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have access to this operation.")
        };
    }
}
