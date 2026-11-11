using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tenancy;

public sealed class CompanyRegistrationReader(BizFlowDbContext db, ITenantContext context) : ICompanyRegistrationReader
{
    public async Task<CompanyRegistrationPage> ListAsync(CompanyRegistrationFilter filter, CancellationToken cancellationToken)
    {
        // Narrow platform registry query, only after Application authorization. Never expose tenant
        // business records or turn the general DbContext filters off for platform identities.
        if (context.UserId is null || context.UserId == Guid.Empty || context.TenantId is not null)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have access to this operation.");
        var query = db.Companies.IgnoreQueryFilters().AsNoTracking();
        if (filter.Status is { } status) query = query.Where(c => c.Status == Enum.Parse<CompanyStatus>(status, true));
        if (filter.Search is { } search)
        {
            // Parameterized literal substring search; SQL wildcard characters have no special meaning.
            var normalized = search.ToUpperInvariant();
            query = query.Where(c => c.Code.Contains(normalized) || c.Name.ToUpper().Contains(normalized));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await (from c in query
                          join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on c.Id equals t.CompanyId into tenants
                          from t in tenants.DefaultIfEmpty()
                          orderby c.CreatedAt descending, c.Id descending
                          select new
                          {
                              c.Id,
                              c.Code,
                              c.Name,
                              c.TaxId,
                              c.ContactPersonName,
                              c.ContactEmail,
                              c.ContactPhone,
                              c.Status,
                              c.CreatedAt,
                              c.EvidenceDocumentKey,
                              c.EvidenceDocumentFileName,
                              TenantKey = t != null ? t.TenantKey : null
                          })
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .ToListAsync(cancellationToken);
        return new(rows.Select(c => new CompanyRegistrationRow(
            c.Id,
            c.Code,
            c.Name,
            c.TaxId,
            c.ContactPersonName,
            c.ContactEmail,
            c.ContactPhone,
            c.Status.ToString().ToUpperInvariant(),
            c.CreatedAt,
            c.EvidenceDocumentKey,
            c.EvidenceDocumentFileName,
            c.EvidenceDocumentKey != null ? $"/api/v1/platform/company-registrations/{c.Id}/document" : null,
            c.TenantKey)).ToArray(),
            filter.Page, filter.PageSize, total);
    }
}
