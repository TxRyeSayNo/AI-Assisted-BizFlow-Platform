using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.Organization;

public sealed record UserDirectoryFilter(int Page = 1, int PageSize = 25, string? Search = null,
    string? Status = null, Guid? DepartmentId = null);
public sealed record UserDirectoryRow(Guid UserId, string EmployeeCode, string FullName, string Email,
    Guid? DepartmentId, string? DepartmentName, string Status);
public sealed record UserDirectoryPage(IReadOnlyList<UserDirectoryRow> Items, int Page, int PageSize, long Total);
public interface IUserDirectoryReader
{
    Task<UserDirectoryPage> ListAsync(Guid tenantId, UserDirectoryFilter filter, CancellationToken cancellationToken);
}

// Canonical API-ORG-01 and SSS §5 Organization read capability. Not user administration.
public sealed class UserDirectoryQuery(ITenantContext context, IResourceAuthorizer authorizer, IUserDirectoryReader reader)
{
    public const string ReadPermission = "users.read";

    public async Task<UserDirectoryPage> ListAsync(UserDirectoryFilter filter, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(ReadPermission, new(context.TenantId), cancellationToken: cancellationToken);
        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "The people directory requires a tenant workspace.");
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            search?.Length > 200 || filter.DepartmentId == Guid.Empty || status is not (null or "ACTIVE" or "INACTIVE" or "LOCKED"))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid search, user status, department and page size from 1 to 100.");
        return await reader.ListAsync(tenantId, filter with { Search = search, Status = status }, cancellationToken);
    }
}
