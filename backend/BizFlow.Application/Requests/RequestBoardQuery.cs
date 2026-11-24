using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

/// <summary>
/// Board-shaped read model for requests. Additive next to <see cref="RequestListQuery"/>, whose
/// filter contract stays untouched, and gated by the very same <see cref="RequestReadPolicy"/>.
///
/// Read-only on purpose: a request moves through nine commands (send, route, receive, process,
/// revise, reject, resolve, confirm, cancel, escalate) and nearly all of them require a payload
/// the board cannot invent, so dragging a request card is not offered. The lanes link to the
/// request detail page, where the existing commands live.
/// </summary>
public sealed record RequestBoardFilter(
    string? Search = null,
    string? Priority = null,
    Guid? ServiceId = null,
    Guid? CategoryId = null,
    Guid? RequesterId = null,
    int ColumnSize = 50);

public sealed record RequestBoardColumn(string Status, IReadOnlyList<RequestListRow> Items, long Total);

public sealed record RequestBoardView(IReadOnlyList<RequestBoardColumn> Columns, int ColumnSize);

public interface IRequestBoardReader
{
    Task<IReadOnlyList<RequestBoardColumn>> ReadAsync(RequestReadScope scope, RequestBoardFilter filter,
        CancellationToken cancellationToken);
}

public sealed class RequestBoardQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IRequestBoardReader reader)
{
    public const int MaxColumnSize = 100;
    public const int MaxSearchLength = 200;

    public async Task<RequestBoardView> GetAsync(RequestBoardFilter filter, CancellationToken cancellationToken)
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
        filter = filter with { Search = Clean(filter.Search), Priority = Clean(filter.Priority) };

        if (filter.ColumnSize is < 1 or > MaxColumnSize ||
            filter.Search?.Length > MaxSearchLength ||
            (filter.Priority is not null &&
                !Enum.GetValues<RequestPriority>().Any(p => RequestListCodes.Priority(p) == filter.Priority)) ||
            filter.ServiceId == Guid.Empty || filter.CategoryId == Guid.Empty || filter.RequesterId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED",
                "Use a valid request priority, search, service, category, requester and column size from 1 to 100.");

        var columns = await reader.ReadAsync(scope, filter, cancellationToken);
        return new(columns, filter.ColumnSize);
    }
}
