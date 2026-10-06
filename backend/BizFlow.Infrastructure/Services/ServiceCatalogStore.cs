using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Services;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Services;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizFlow.Infrastructure.Services;

public sealed class ServiceCatalogStore(BizFlowDbContext db, ITenantContext context) : IServiceCatalogStore
{
    private void AssertTenant(Guid tenant)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenant == Guid.Empty || context.TenantId != tenant)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Services require a tenant workspace.");
    }
    public async Task<ServicePage> ListAsync(Guid tenantId, ServiceFilter filter, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var query = db.Services.AsNoTracking().Where(s => s.TenantId == tenantId);
        if (filter.Search is { } search)
        { var value = search.ToUpperInvariant(); query = query.Where(s => s.Name.ToUpper().Contains(value) || s.Code.ToUpper().Contains(value)); }
        if (filter.Status is { } status)
        { var value = Enum.Parse<ServiceStatus>(status, true); query = query.Where(s => s.Status == value); }
        var total = await query.LongCountAsync(cancellationToken);
        var services = await query.OrderBy(s => s.Code).ThenBy(s => s.Id).Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(s => new { Service = s, Version = EF.Property<uint>(s, "Version") }).ToListAsync(cancellationToken);
        var ids = services.Select(s => s.Service.Id).ToArray();
        var categories = await db.ServiceCategories.AsNoTracking().Where(c => ids.Contains(c.ServiceId)).OrderBy(c => c.Code).ThenBy(c => c.Id).ToListAsync(cancellationToken);
        var items = services.Select(s => Row(s.Service, categories.Where(c => c.ServiceId == s.Service.Id), s.Version)).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(items, filter.Page, filter.PageSize, total);
    }
    public async Task<ServiceRow> CreateAsync(InternalService service, IReadOnlyList<ServiceCategory> categories, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(service.TenantId);
        if (categories.Any(c => c.ServiceId != service.Id) || audit.TenantId != service.TenantId || audit.ObjectId != service.Id)
            throw new InvalidOperationException("Service, categories and audit must form one tenant-owned change.");
        db.AddRange(service, audit); db.ServiceCategories.AddRange(categories);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException failure) when (failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Service_TenantId_Code" })
        { throw new ApplicationFault(FaultKind.Conflict, "SERVICE.DUPLICATE_CODE", "A service with this code already exists in this workspace."); }
        return Row(service, categories.OrderBy(c => c.Code, StringComparer.Ordinal), db.Entry(service).Property<uint>("Version").CurrentValue);
    }
    internal static ServiceRow Row(InternalService service, IEnumerable<ServiceCategory> categories, uint version) => new(service.Id, service.Code, service.Name,
        service.Description, service.Status.ToString().ToUpperInvariant(), service.ActiveWorkflowVersionId, service.ActiveSlaVersionId, service.CreatedAt,
        categories.Select(c => new ServiceCategoryRow(c.Id, c.Code, c.Name, c.Status.ToString().ToUpperInvariant())).ToArray(), ServiceETag.Format(version));
}
