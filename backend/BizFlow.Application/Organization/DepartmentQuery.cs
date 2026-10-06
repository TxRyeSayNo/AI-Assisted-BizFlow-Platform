using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.Organization;

public sealed record DepartmentFilter(int Page = 1, int PageSize = 25, string? Status = null, string? Search = null);
public sealed record DepartmentRow(Guid DepartmentId, string Code, string Name, Guid? ParentDepartmentId, string Status, DateTimeOffset CreatedAt);
public sealed record DepartmentPage(IReadOnlyList<DepartmentRow> Items, int Page, int PageSize, long Total);
public interface IDepartmentReader
{
    Task<DepartmentPage> ListAsync(Guid tenantId, DepartmentFilter filter, CancellationToken cancellationToken);
}

public sealed class DepartmentQuery(TenantMembershipAuthorizer membership, IDepartmentReader reader)
{
    public async Task<DepartmentPage> ListAsync(DepartmentFilter filter, CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("departments.read", cancellationToken);
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            search?.Length > 200 || status is not (null or "ACTIVE" or "INACTIVE"))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid department status, search text and page size from 1 to 100.");
        return await reader.ListAsync(tenantId, filter with { Search = search, Status = status }, cancellationToken);
    }
}
