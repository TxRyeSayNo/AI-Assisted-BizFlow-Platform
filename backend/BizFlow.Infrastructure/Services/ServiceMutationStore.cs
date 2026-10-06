using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Services;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Services;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BizFlow.Infrastructure.Services;

public sealed class ServiceMutationStore(BizFlowDbContext db, ITenantContext context) : IServiceMutationStore
{
    public async Task<IServiceMutationTransaction?> BeginAsync(Guid tenantId, Guid serviceId, CancellationToken cancellationToken)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenantId == Guid.Empty || context.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Service mutations require a tenant workspace.");
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var service = await db.Services.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Service" WHERE "ServiceId"={serviceId} AND "TenantId"={tenantId} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            if (service is null) { await transaction.DisposeAsync(); return null; }
            var categories = await db.ServiceCategories.Where(c => c.ServiceId == serviceId).ToListAsync(cancellationToken);
            return new MutationTransaction(db, transaction, service, categories);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private sealed class MutationTransaction(BizFlowDbContext db, IDbContextTransaction transaction,
        InternalService service, IReadOnlyList<ServiceCategory> categories) : IServiceMutationTransaction
    {
        public InternalService Service => service;
        public uint Version => db.Entry(service).Property<uint>("Version").CurrentValue;
        public IReadOnlyList<ServiceCategory> Categories => categories;
        public Task<ServiceRow> CommitAsync(ServiceCategory category, AuditLog audit, CancellationToken cancellationToken)
        {
            if (category.ServiceId != service.Id || audit.TenantId != service.TenantId || audit.ObjectId != category.Id)
                throw new InvalidOperationException("Category and audit must belong to the locked service.");
            db.ServiceCategories.Add(category);
            return PersistAsync(audit, categories.Append(category), cancellationToken);
        }
        public Task<ServiceRow> CommitMetadataAsync(AuditLog audit, CancellationToken cancellationToken)
        {
            if (audit.TenantId != service.TenantId || audit.ObjectId != service.Id)
                throw new InvalidOperationException("Metadata audit must belong to the locked service.");
            return PersistAsync(audit, categories, cancellationToken);
        }
        private async Task<ServiceRow> PersistAsync(AuditLog audit, IEnumerable<ServiceCategory> updatedCategories, CancellationToken cancellationToken)
        {
            // Advance xmin even when a fixed clock produces an unchanged UpdatedAt value.
            db.Entry(service).Property(s => s.UpdatedAt).IsModified = true;
            db.AuditLogs.Add(audit);
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException failure) when (failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_ServiceCategory_ServiceId_Code" })
            { throw new ApplicationFault(FaultKind.Conflict, "SERVICE.CATEGORY_CODE_EXISTS", "A category with this code already exists in this service."); }
            catch (DbUpdateException failure) when (failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Service_TenantId_Code" })
            { throw new ApplicationFault(FaultKind.Conflict, "SERVICE.DUPLICATE_CODE", "A service with this code already exists in this workspace."); }
            var result = ServiceCatalogStore.Row(service, updatedCategories.OrderBy(c => c.Code, StringComparer.Ordinal), Version);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
