using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Requests;

namespace BizFlow.Application.Requests;

public sealed record RequestEscalationView(
    Guid Id,
    Guid RequestId,
    string State,
    Guid EscalatedById,
    string EscalatedByName,
    string Reason,
    DateTimeOffset EscalatedAt,
    Guid? DecidedById,
    string? DecidedByName,
    string? DecisionNote,
    DateTimeOffset? DecidedAt);

public sealed record PendingEscalationItem(
    Guid RequestId,
    string Title,
    string RequestState,
    Guid EscalationId,
    Guid EscalatedById,
    string EscalatedByName,
    string Reason,
    DateTimeOffset EscalatedAt);

public interface IRequestEscalationStore
{
    Task<RequestState?> GetRequestStateAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken);
    Task<RequestEscalation?> GetLatestAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken);
    Task<RequestEscalationView?> GetLatestViewAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken);
    Task AddAndSaveAsync(RequestEscalation escalation, CancellationToken cancellationToken);
    Task UpdateAndSaveAsync(RequestEscalation escalation, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingEscalationItem>> ListPendingAsync(Guid tenantId, CancellationToken cancellationToken);
}

// Additive approval layer: it reads the request state but never mutates the WorkRequest
// state machine. A manager (requests.escalate) pushes a request up; an elevated approver
// (requests.approve.elevated) decides. Employees lack requests.escalate, so they cannot skip a level.
public sealed class RequestEscalationCommand(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IRequestEscalationStore store,
    TimeProvider clock)
{
    private static readonly RequestState[] NonEscalatable =
    {
        RequestState.Draft, RequestState.Confirmed, RequestState.Closed,
        RequestState.Rejected, RequestState.Cancelled
    };

    public async Task<RequestEscalationView> EscalateAsync(Guid requestId, string reason, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.escalate", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ApplicationFault(FaultKind.Validation, "REQUEST.ESCALATION_REASON_REQUIRED", "Vui lòng nhập lý do chuyển lên cấp trên.");

        var state = await store.GetRequestStateAsync(tenantId, requestId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        if (Array.IndexOf(NonEscalatable, state) >= 0)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.ESCALATION_INVALID_STATE", "Yêu cầu ở trạng thái này không thể chuyển lên cấp trên để duyệt.");

        var latest = await store.GetLatestAsync(tenantId, requestId, cancellationToken);
        if (latest is { State: RequestEscalationState.Pending })
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.ESCALATION_ALREADY_PENDING", "Yêu cầu này đang chờ cấp trên duyệt.");

        var now = clock.GetUtcNow();
        var escalation = RequestEscalation.Create(tenantId, requestId, actorId, reason, now);
        await store.AddAndSaveAsync(escalation, cancellationToken);
        return (await store.GetLatestViewAsync(tenantId, requestId, cancellationToken))!;
    }

    public async Task<RequestEscalationView> DecideAsync(Guid requestId, bool approve, string? note, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.approve.elevated", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw ApplicationFault.NotFound();

        var latest = await store.GetLatestAsync(tenantId, requestId, cancellationToken);
        if (latest is null || latest.State != RequestEscalationState.Pending)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.ESCALATION_NOT_PENDING", "Không có yêu cầu nào đang chờ duyệt ở cấp trên.");

        var now = clock.GetUtcNow();
        latest.Decide(approve, actorId, note, now);
        await store.UpdateAndSaveAsync(latest, cancellationToken);
        return (await store.GetLatestViewAsync(tenantId, requestId, cancellationToken))!;
    }

    public async Task<IReadOnlyList<PendingEscalationItem>> ListPendingAsync(CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("requests.approve.elevated", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId)
            throw ApplicationFault.NotFound();
        return await store.ListPendingAsync(tenantId, cancellationToken);
    }

    public async Task<RequestEscalationView?> GetLatestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId)
            throw ApplicationFault.NotFound();
        return await store.GetLatestViewAsync(tenantId, requestId, cancellationToken);
    }
}
