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
        var rows = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(c => new { c.Id, c.Code, c.Name, c.ContactEmail, c.Status, c.CreatedAt }).ToListAsync(cancellationToken);
        return new(rows.Select(c => new CompanyRegistrationRow(c.Id, c.Code, c.Name, c.ContactEmail,
            c.Status.ToString().ToUpperInvariant(), c.CreatedAt)).ToArray(), filter.Page, filter.PageSize, total);
    }
}
