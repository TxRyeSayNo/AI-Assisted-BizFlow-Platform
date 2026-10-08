using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RequestListFilter(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Status = null,
    string? Priority = null,
    Guid? ServiceId = null,
    Guid? CategoryId = null);

public sealed record RequestListRow(
    Guid RequestId,
    string Title,
    string Status,
    string Priority,
    Guid ServiceId,
    string? ServiceName,
    Guid CategoryId,
    string? CategoryName,
    Guid RequesterId,
    string? RequesterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RequestListPage(IReadOnlyList<RequestListRow> Items, int Page, int PageSize, long Total);

public interface IRequestListReader
{
    Task<RequestListPage> ListAsync(RequestReadScope scope, RequestListFilter filter, CancellationToken cancellationToken);
}

public static class RequestListCodes
{
    public static string State(RequestState state) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(state.ToString());
    public static string Priority(RequestPriority priority) => priority.ToString().ToUpperInvariant();
}

public sealed class RequestListQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IRequestListReader reader)
{
    public async Task<RequestListPage> ListAsync(RequestListFilter filter, CancellationToken cancellationToken)
    {
        var access = context.UserId is { } user && user != Guid.Empty
            ? await snapshots.ResolveAsync(user, context.TenantId, cancellationToken)
            : null;

        var (decision, scope) = access is null || access.UserId != context.UserId || access.TenantId != context.TenantId
            ? (AccessDecision.Deny(AccessDenial.Unauthenticated), (RequestReadScope?)null)
            : RequestReadPolicy.Resolve(access);

        if (!decision.Allowed || scope is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, "requests.read", decision.Denial, cancellationToken);
            throw decision.Denial == AccessDenial.Unauthenticated
                ? new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.")
                : new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You do not have access to requests.");
        }

        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        filter = filter with { Search = Clean(filter.Search), Status = Clean(filter.Status), Priority = Clean(filter.Priority) };

        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            filter.Search?.Length > 200 ||
            (filter.Status is not null && !Enum.GetValues<RequestState>().Any(s => RequestListCodes.State(s) == filter.Status)) ||
            (filter.Priority is not null && !Enum.GetValues<RequestPriority>().Any(p => RequestListCodes.Priority(p) == filter.Priority)))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid request status, priority, search and page size from 1 to 100.");

        return await reader.ListAsync(scope, filter, cancellationToken);
    }
}
