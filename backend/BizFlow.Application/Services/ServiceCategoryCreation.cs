using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Services;

namespace BizFlow.Application.Services;

public sealed class ServiceCategoryCreation(ITenantContext context, IResourceAuthorizer authorizer,
    ISecurityAuditWriter audit, IServiceMutationStore store, TimeProvider clock)
{
    private Task AuthorizeAsync(CancellationToken cancellationToken) => authorizer.AuthorizeAsync(ServiceCatalog.CreatePermission,
        new(context.TenantId), cancellationToken: cancellationToken);

    public async Task<ServiceRow> CreateAsync(Guid serviceId, string? ifMatch, CreateCategory input, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Service categories require a tenant workspace.");
        await using var transaction = await store.BeginAsync(tenantId, serviceId, cancellationToken);
        if (transaction is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, tenantId, ServiceCatalog.CreatePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        await AuthorizeAsync(cancellationToken); // Grants may change while waiting for the aggregate lock.
        ServiceETag.RequireCurrent(ifMatch, transaction);
        ServiceCategory category;
        try { category = ServiceCategory.Create(transaction.Service.Id, input.Code, input.Name, input.Active); }
        catch (ArgumentException) { throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a nonblank code up to 80 characters and a name up to 200 characters without control characters."); }
        if (transaction.Categories.Any(c => string.Equals(c.Code, category.Code, StringComparison.Ordinal)))
            throw new ApplicationFault(FaultKind.Conflict, "SERVICE.CATEGORY_CODE_EXISTS", "A category with this code already exists in this service.");
        var now = clock.GetUtcNow();
        transaction.Service.RecordCategoryAdded(category, now);
        return await transaction.CommitAsync(category, AuditLog.ServiceCategoryCreated(tenantId, context.UserId!.Value, category, now), cancellationToken);
    }
}
