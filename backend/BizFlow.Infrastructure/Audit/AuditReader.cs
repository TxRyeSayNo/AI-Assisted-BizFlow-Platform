using System.Text.Json;
using BizFlow.Application.Audit;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Audit;

public sealed class AuditReader(BizFlowDbContext db, ITenantContext context) : IAuditReader
{
    public async Task<AuditPage> ListAsync(Guid? tenantId, AuditCriteria criteria, CancellationToken cancellationToken)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || context.TenantId != tenantId || tenantId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Audit access requires an authorized workspace or platform context.");
        // Narrow platform bypass, after Application authorization, restricted to NULL-tenant events.
        // Platform access never means reading all company audit streams.
        var query = tenantId is null ? db.AuditLogs.IgnoreQueryFilters().Where(a => a.TenantId == null) : db.AuditLogs.Where(a => a.TenantId == tenantId);
        query = query.AsNoTracking();
        if (criteria.ActorId is { } actor) query = query.Where(a => a.ActorId == actor);
        if (criteria.ActorType is { } actorType)
        {
            var value = actorType switch { "USER" => AuditActorType.User, "SYSTEM" => AuditActorType.System, _ => AuditActorType.AiAgent };
            query = query.Where(a => a.ActorType == value);
        }
        if (criteria.Action is { } action) query = query.Where(a => a.Action == action);
        if (criteria.ObjectType is { } objectType) query = query.Where(a => a.ObjectType == objectType);
        if (criteria.ObjectId is { } objectId) query = query.Where(a => a.ObjectId == objectId);
        if (criteria.From is { } from) query = query.Where(a => a.CreatedAt >= from);
        if (criteria.Until is { } until) query = query.Where(a => a.CreatedAt < until);
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(checked((criteria.Page - 1) * criteria.PageSize)).Take(criteria.PageSize)
            .Select(a => new { a.Id, a.ActorType, a.ActorId, a.Action, a.ObjectType, a.ObjectId, a.BeforeJson, a.AfterJson, a.MetadataJson, a.CreatedAt })
            .ToListAsync(cancellationToken);
        return new(rows.Select(a => new AuditRow(a.Id, a.ActorType switch { AuditActorType.User => "USER", AuditActorType.System => "SYSTEM", _ => "AI_AGENT" },
            a.ActorId, a.Action, a.ObjectType, a.ObjectId, Json(a.BeforeJson), Json(a.AfterJson), Json(a.MetadataJson), a.CreatedAt)).ToArray(),
            criteria.Page, criteria.PageSize, total);
    }

    private static JsonElement? Json(string? value)
    {
        if (value is null) return null;
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
