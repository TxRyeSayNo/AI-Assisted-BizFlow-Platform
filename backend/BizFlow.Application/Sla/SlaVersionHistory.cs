using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Sla;

public sealed record SlaCalendarView(Guid CalendarId, string TimeZone, JsonElement WorkingHours, JsonElement Holidays);
public sealed record SlaVersionHistoryRow(Guid SlaVersionId, int VersionNo, int TargetMinutes, int WarningMinutes,
    JsonElement? EscalationConfig, SlaCalendarView Calendar);
public sealed record SlaVersionHistoryPage(Guid SlaProfileId, string ProfileName, IReadOnlyList<SlaVersionHistoryRow> Items, int Page, int PageSize, long Total);
public interface ISlaVersionHistoryStore
{
    Task<SlaVersionHistoryPage?> ReadAsync(Guid tenantId, Guid profileId, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class SlaVersionHistory(ITenantContext context, IResourceAuthorizer authorizer,
    ISecurityAuditWriter audit, ISlaVersionHistoryStore store)
{
    public async Task<SlaVersionHistoryPage> ReadAsync(Guid profileId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(SlaProfileCatalog.ReadPermission, new(context.TenantId), cancellationToken: cancellationToken);
        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "SLA history requires a tenant workspace.");
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a positive page and a page size from 1 to 100.");
        var result = await store.ReadAsync(tenantId, profileId, page, pageSize, cancellationToken);
        if (result is not null) return result;
        await audit.RecordDeniedAccessAsync(context.UserId, tenantId, SlaProfileCatalog.ReadPermission, AccessDenial.ResourceNotFound, cancellationToken);
        throw ApplicationFault.NotFound();
    }
}
