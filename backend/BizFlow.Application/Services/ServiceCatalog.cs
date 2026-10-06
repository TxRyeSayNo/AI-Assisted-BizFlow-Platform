using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Services;

namespace BizFlow.Application.Services;

public sealed record ServiceFilter(int Page = 1, int PageSize = 25, string? Search = null, string? Status = null);
public sealed record CreateCategory(string Code, string Name, bool Active = true);
public sealed record CreateService(string Code, string Name, string? Description, bool Active, IReadOnlyList<CreateCategory>? Categories);
public sealed record ServiceCategoryRow(Guid ServiceCategoryId, string Code, string Name, string Status);
public sealed record ServiceRow(Guid ServiceId, string Code, string Name, string? Description, string Status,
    Guid? ActiveWorkflowVersionId, Guid? ActiveSlaVersionId, DateTimeOffset CreatedAt, IReadOnlyList<ServiceCategoryRow> Categories, string ETag);
public sealed record ServicePage(IReadOnlyList<ServiceRow> Items, int Page, int PageSize, long Total);
public interface IServiceCatalogStore
{
    Task<ServicePage> ListAsync(Guid tenantId, ServiceFilter filter, CancellationToken cancellationToken);
    Task<ServiceRow> CreateAsync(InternalService service, IReadOnlyList<ServiceCategory> categories, AuditLog audit, CancellationToken cancellationToken);
}

public sealed class ServiceCatalog(ITenantContext context, TenantMembershipAuthorizer membership, IResourceAuthorizer authorizer,
    IServiceCatalogStore store, TimeProvider clock)
{
    public const string CreatePermission = "service.create";
    public async Task<ServicePage> ListAsync(ServiceFilter filter, CancellationToken cancellationToken)
    {
        // Canonical API-SVC-01 allows authenticated tenant members, not only administrators.
        var tenant = await membership.RequireAsync("service.read", cancellationToken);
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            search?.Length > 200 || status is not (null or "DRAFT" or "ACTIVE" or "INACTIVE"))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a supported status, search up to 200 characters and a page size from 1 to 100.");
        return await store.ListAsync(tenant, filter with { Search = search, Status = status }, cancellationToken);
    }
    public async Task<ServiceRow> CreateAsync(CreateService input, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(CreatePermission, new(context.TenantId), cancellationToken: cancellationToken);
        var tenant = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Services require a tenant workspace.");
        InternalService service; ServiceCategory[] categories; var now = clock.GetUtcNow();
        try
        {
            service = InternalService.Create(tenant, input.Code, input.Name, input.Description, input.Active, now);
            categories = (input.Categories ?? []).Select(c => c is null ? throw new ArgumentException("Category required.") :
                ServiceCategory.Create(service.Id, c.Code, c.Name, c.Active)).ToArray();
        }
        catch (ArgumentException) { throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use nonblank codes up to 80 characters, names up to 200 characters and plain-text descriptions."); }
        if (categories.Select(c => c.Code).Distinct(StringComparer.Ordinal).Count() != categories.Length)
            throw new ApplicationFault(FaultKind.Validation, "SERVICE.DUPLICATE_CATEGORY_CODE", "Category codes must be unique within the service.");
        return await store.CreateAsync(service, categories, AuditLog.ServiceCreated(tenant, context.UserId!.Value, service, categories, now), cancellationToken);
    }
}
