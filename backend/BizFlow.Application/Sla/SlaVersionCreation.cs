using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Sla;

namespace BizFlow.Application.Sla;

public sealed record CreateSlaVersion(int TargetMinutes, int WarningMinutes, Guid CalendarId, JsonElement? EscalationConfig = null);
public sealed record SlaVersionView(Guid SlaVersionId, Guid SlaProfileId, int VersionNo, int TargetMinutes, int WarningMinutes, Guid CalendarId, JsonElement? EscalationConfig);

public interface ISlaVersionStore
{
    Task<ISlaVersionTransaction?> BeginAsync(Guid tenantId, Guid profileId, Guid calendarId, CancellationToken cancellationToken);
}
public interface ISlaVersionTransaction : IAsyncDisposable
{
    SlaProfile Profile { get; }
    BusinessCalendar Calendar { get; }
    int LatestVersionNo { get; }
    Task<bool> LockActiveRecipientsAsync(IReadOnlyList<Guid> recipients, CancellationToken cancellationToken);
    Task CommitAsync(SlaVersion version, AuditLog audit, CancellationToken cancellationToken);
}

public sealed class SlaVersionCreation(ITenantContext context, IResourceAuthorizer authorizer, ISecurityAuditWriter securityAudit,
    ISlaVersionStore store, TimeProvider clock)
{
    private Task AuthorizeAsync(CancellationToken cancellationToken) => authorizer.AuthorizeAsync(SlaProfileCatalog.ConfigurePermission,
        new(context.TenantId), cancellationToken: cancellationToken);

    public async Task<SlaVersionView> CreateAsync(Guid profileId, CreateSlaVersion input, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "SLA versions require a tenant workspace.");
        if (input.TargetMinutes <= 0 || input.WarningMinutes < 0 || input.WarningMinutes >= input.TargetMinutes)
            throw new ApplicationFault(FaultKind.Validation, "SLA.INVALID_THRESHOLD", "Use a positive target and a nonnegative warning strictly below the target.");
        if (input.CalendarId == Guid.Empty || profileId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A profile and calendar identifier are required.");
        var json = input.EscalationConfig is null || input.EscalationConfig.Value.ValueKind == JsonValueKind.Null ? null : input.EscalationConfig.Value.GetRawText();
        // Validate the typed policy before acquiring database locks. The real number is assigned under the parent lock.
        SlaVersion validated;
        try { validated = SlaVersion.CreateSnapshot(profileId, 1, input.TargetMinutes, input.WarningMinutes, input.CalendarId, json); }
        catch (ArgumentException) { throw new ApplicationFault(FaultKind.Validation, "SLA.INVALID_ESCALATION", "Use ordered escalation levels with increasing offsets and valid recipient UUID lists."); }
        await using var transaction = await store.BeginAsync(tenantId, profileId, input.CalendarId, cancellationToken);
        if (transaction is null)
        {
            await securityAudit.RecordDeniedAccessAsync(context.UserId, context.TenantId, SlaProfileCatalog.ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        await AuthorizeAsync(cancellationToken); // Authority may have changed while waiting for either configuration lock.
        if (!transaction.Calendar.HasWorkingTime())
            throw new ApplicationFault(FaultKind.Validation, "SLA.INVALID_CALENDAR", "An SLA version requires a calendar with working time.");
        if (transaction.LatestVersionNo == int.MaxValue)
            throw new ApplicationFault(FaultKind.Conflict, "SLA.VERSION_LIMIT", "This profile has reached the supported version-number limit.");
        var recipientsValid = await transaction.LockActiveRecipientsAsync(validated.EscalationRecipientIds(), cancellationToken);
        await AuthorizeAsync(cancellationToken); // Also recheck after a concurrent recipient update has completed.
        if (!recipientsValid)
            throw new ApplicationFault(FaultKind.Validation, "SLA.INVALID_ESCALATION_TARGET", "Escalation recipients must be active users in this tenant.");
        var version = SlaVersion.CreateSnapshot(profileId, transaction.LatestVersionNo + 1, input.TargetMinutes, input.WarningMinutes, input.CalendarId, json);
        await transaction.CommitAsync(version, AuditLog.SlaVersionCreated(tenantId, context.UserId!.Value, version, clock.GetUtcNow()), cancellationToken);
        return new(version.Id, version.SlaProfileId, version.VersionNo, version.TargetMinutes, version.WarningMinutes, version.CalendarId,
            json is null ? null : JsonSerializer.Deserialize<JsonElement>(json));
    }
}
