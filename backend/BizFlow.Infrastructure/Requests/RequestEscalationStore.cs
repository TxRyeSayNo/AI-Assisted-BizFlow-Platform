using BizFlow.Application.Requests;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestEscalationStore(BizFlowDbContext db) : IRequestEscalationStore
{
    public async Task<RequestState?> GetRequestStateAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        var rows = await db.Requests.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Id == requestId)
            .Select(r => (RequestState?)r.Status)
            .ToListAsync(cancellationToken);
        return rows.Count > 0 ? rows[0] : null;
    }

    public Task<RequestEscalation?> GetLatestAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken) =>
        db.RequestEscalations
            .Where(e => e.TenantId == tenantId && e.RequestId == requestId)
            .OrderByDescending(e => e.EscalatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<RequestEscalationView?> GetLatestViewAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        var row = await db.RequestEscalations.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.RequestId == requestId)
            .OrderByDescending(e => e.EscalatedAt)
            .Select(e => new
            {
                e.Id,
                e.RequestId,
                e.State,
                e.EscalatedById,
                EscalatedByName = db.Users.Where(u => u.Id == e.EscalatedById).Select(u => u.FullName).FirstOrDefault(),
                e.Reason,
                e.EscalatedAt,
                e.DecidedById,
                DecidedByName = e.DecidedById == null ? null : db.Users.Where(u => u.Id == e.DecidedById).Select(u => u.FullName).FirstOrDefault(),
                e.DecisionNote,
                e.DecidedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new RequestEscalationView(row.Id, row.RequestId, row.State.ToString().ToUpperInvariant(),
                row.EscalatedById, row.EscalatedByName ?? "", row.Reason, row.EscalatedAt,
                row.DecidedById, row.DecidedByName, row.DecisionNote, row.DecidedAt);
    }

    public async Task AddAndSaveAsync(RequestEscalation escalation, CancellationToken cancellationToken)
    {
        db.RequestEscalations.Add(escalation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAndSaveAsync(RequestEscalation escalation, CancellationToken cancellationToken)
    {
        // The entity is already tracked (loaded via GetLatestAsync), so only the fields changed by
        // Decide() are marked modified. Calling Update() would re-mark identity/ownership columns and
        // trip the immutable-field guard, so we just persist the tracked changes.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingEscalationItem>> ListPendingAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await db.RequestEscalations.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.State == RequestEscalationState.Pending)
            .OrderBy(e => e.EscalatedAt)
            .Select(e => new
            {
                e.RequestId,
                Title = db.Requests.Where(r => r.Id == e.RequestId).Select(r => r.Title).FirstOrDefault(),
                RequestStatus = db.Requests.Where(r => r.Id == e.RequestId).Select(r => (RequestState?)r.Status).FirstOrDefault(),
                EscalationId = e.Id,
                e.EscalatedById,
                EscalatedByName = db.Users.Where(u => u.Id == e.EscalatedById).Select(u => u.FullName).FirstOrDefault(),
                e.Reason,
                e.EscalatedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PendingEscalationItem(
            r.RequestId, r.Title ?? "", (r.RequestStatus ?? RequestState.Submitted).ToString().ToUpperInvariant(),
            r.EscalationId, r.EscalatedById, r.EscalatedByName ?? "", r.Reason, r.EscalatedAt)).ToList();
    }
}
