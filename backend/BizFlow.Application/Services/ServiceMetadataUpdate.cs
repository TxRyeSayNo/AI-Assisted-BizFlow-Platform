using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Services;

public sealed record UpdateService(string Code, string Name, string? Description, bool Active);

public sealed class ServiceMetadataUpdate(ITenantContext context, IResourceAuthorizer authorizer,
    ISecurityAuditWriter audit, IServiceMutationStore store, TimeProvider clock)
{
    public const string Permission = "service.update";
    private Task AuthorizeAsync(CancellationToken cancellationToken) => authorizer.AuthorizeAsync(Permission,
        new(context.TenantId), cancellationToken: cancellationToken);
    public async Task<ServiceRow> UpdateAsync(Guid serviceId, string? ifMatch, UpdateService input, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Services require a tenant workspace.");
        await using var transaction = await store.BeginAsync(tenantId, serviceId, cancellationToken);
        if (transaction is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, tenantId, Permission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        await AuthorizeAsync(cancellationToken);
        ServiceETag.RequireCurrent(ifMatch, transaction);
        var before = transaction.Service.Metadata(); var now = clock.GetUtcNow();
        try { transaction.Service.UpdateMetadata(input.Code, input.Name, input.Description, input.Active, now); }
        catch (ArgumentException) { throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a nonblank code up to 80 characters, name up to 200 characters and a plain-text description."); }
        return await transaction.CommitMetadataAsync(AuditLog.ServiceMetadataUpdated(tenantId, context.UserId!.Value,
            transaction.Service, before, now), cancellationToken);
    }
}
