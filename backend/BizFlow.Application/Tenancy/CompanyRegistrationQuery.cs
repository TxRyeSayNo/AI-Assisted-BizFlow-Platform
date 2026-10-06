using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Tenancy;

public sealed record CompanyRegistrationFilter(int Page = 1, int PageSize = 25, string? Status = null, string? Search = null);
public sealed record CompanyRegistrationRow(Guid CompanyId, string Code, string Name, string ContactEmail, string Status, DateTimeOffset CreatedAt);
public sealed record CompanyRegistrationPage(IReadOnlyList<CompanyRegistrationRow> Items, int Page, int PageSize, long Total);
public interface ICompanyRegistrationReader
{
    Task<CompanyRegistrationPage> ListAsync(CompanyRegistrationFilter query, CancellationToken cancellationToken);
}

public sealed class CompanyRegistrationQuery(IResourceAuthorizer authorizer, ICompanyRegistrationReader reader)
{
    public async Task<CompanyRegistrationPage> ListAsync(CompanyRegistrationFilter query, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(PlatformPermissions.ReadCompanyRegistrations, new ResourceScope(null), cancellationToken: cancellationToken);
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim();
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            search?.Length > 200 || status is not (null or "PENDING" or "ACTIVE" or "SUSPENDED" or "INACTIVE"))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid status, search text and page size from 1 to 100.");
        return await reader.ListAsync(query with { Status = status, Search = search }, cancellationToken);
    }
}
