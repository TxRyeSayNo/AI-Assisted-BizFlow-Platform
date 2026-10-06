using BizFlow.Domain.Security;

namespace BizFlow.Application.Security;

public interface IAccessSnapshotProvider
{
    // Re-read account/tenant activity and grants from authoritative storage.
    Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken);
}

public interface ISecurityAuditWriter
{
    Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission,
        AccessDenial reason, CancellationToken cancellationToken);
}

public interface IResourceAuthorizer
{
    Task AuthorizeAsync(string permission, ResourceScope resource,
        Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default);
}
