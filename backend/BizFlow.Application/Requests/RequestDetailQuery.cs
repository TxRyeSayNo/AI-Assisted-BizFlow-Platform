using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Requests;

public sealed record RequestDetailView(
    Guid RequestId,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid ServiceId,
    string? ServiceName,
    Guid CategoryId,
    string? CategoryName,
    Guid RequesterId,
    string? RequesterName,
    Guid? ParentRequestId,
    Guid? RevisedFromRequestId,
    Guid? WorkflowVersionId,
    Guid? SlaVersionId,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CurrentRoutingDepartmentId = null,
    string? CurrentRoutingDepartmentName = null,
    Guid? CurrentRoutingUserId = null,
    string? CurrentRoutingUserName = null,
    DateTimeOffset? RoutedAt = null,
    DateTimeOffset? ReceivedAt = null,
    string? RejectionReason = null,
    IReadOnlyList<RequestResolutionItemView>? Resolutions = null,
    IReadOnlyList<RequestConfirmationItemView>? Confirmations = null,
    IReadOnlyList<RequestLinkedTaskItemView>? Tasks = null);

public sealed record RequestResolutionItemView(
    Guid ResolutionId,
    Guid ResolverId,
    string? ResolverName,
    string Content,
    int RevisionNo,
    DateTimeOffset CreatedAt);

public sealed record RequestConfirmationItemView(
    Guid ConfirmationId,
    Guid ActorId,
    string? ActorName,
    string MilestoneType,
    string Decision,
    string? Note,
    DateTimeOffset ConfirmedAt);

public sealed record RequestLinkedTaskItemView(
    Guid TaskId,
    string Title,
    string Status,
    string Priority,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    Guid? AssignedUserId,
    string? AssignedUserName,
    Guid? AssignedDepartmentId,
    string? AssignedDepartmentName);

public interface IRequestDetailReader
{
    Task<RequestDetailView?> GetAsync(RequestReadScope scope, Guid id, CancellationToken cancellationToken);
}

public sealed class RequestDetailQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IRequestDetailReader reader)
{
    public async Task<RequestDetailView> GetAsync(Guid id, CancellationToken cancellationToken)
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
            if (context.UserId is null) throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
            throw ApplicationFault.NotFound();
        }

        var result = await reader.GetAsync(scope, id, cancellationToken);
        if (result is not null) return result;

        await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, "requests.read", AccessDenial.ResourceNotFound, cancellationToken);
        throw ApplicationFault.NotFound();
    }
}
