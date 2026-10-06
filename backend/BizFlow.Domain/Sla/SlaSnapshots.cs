using System.Text.Json;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.Sla;

// Calendar and escalation shapes follow ADR-0005/0006. Runtime calculation and
// authorized audited Application orchestration remain separate requirements.
public sealed class BusinessCalendar
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public string WorkingHoursJson { get; private set; } = "{}";
    public string HolidaysJson { get; private set; } = "[]";
    private BusinessCalendar() { }

    public static BusinessCalendar Create(Guid tenantId, string timeZone = "UTC", string workingHoursJson = "{}", string holidaysJson = "[]")
    {
        var zone = EntityRules.Text(timeZone, 64, nameof(timeZone));
        if (zone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(zone, out _))
            throw new ArgumentException("An IANA timezone is required.", nameof(timeZone));
        CalendarConfiguration.Validate(workingHoursJson, holidaysJson);
        return new() { Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)), TimeZone = zone,
            WorkingHoursJson = SlaJson.Container(workingHoursJson, JsonValueKind.Object),
            HolidaysJson = SlaJson.Container(holidaysJson, JsonValueKind.Array) };
    }
    public bool HasWorkingTime() => CalendarConfiguration.HasWorkingTime(WorkingHoursJson);
}

public sealed class SlaVersion
{
    public Guid Id { get; private set; }
    public Guid SlaProfileId { get; private set; }
    public int VersionNo { get; private set; }
    public int TargetMinutes { get; private set; }
    public int WarningMinutes { get; private set; }
    public Guid CalendarId { get; private set; }
    public string? EscalationConfigJson { get; private set; }
    private SlaVersion() { }

    public static SlaVersion CreateSnapshot(Guid profileId, int versionNo, int targetMinutes, int warningMinutes, Guid calendarId, string? escalationConfigJson = null)
    {
        if (versionNo < 1) throw new ArgumentOutOfRangeException(nameof(versionNo));
        // Approved A-08 resolves the dictionary/validation discrepancy.
        if (targetMinutes <= 0 || warningMinutes < 0 || warningMinutes >= targetMinutes)
            throw new ArgumentException("SLA targets must be positive and warnings strictly below the target.");
        EscalationConfiguration.Validate(escalationConfigJson);
        return new() { Id = Guid.CreateVersion7(), SlaProfileId = EntityRules.Id(profileId, nameof(profileId)), VersionNo = versionNo,
            TargetMinutes = targetMinutes, WarningMinutes = warningMinutes, CalendarId = EntityRules.Id(calendarId, nameof(calendarId)),
            EscalationConfigJson = escalationConfigJson is null ? null : SlaJson.Container(escalationConfigJson, JsonValueKind.Object) };
    }
    public IReadOnlyList<Guid> EscalationRecipientIds() => EscalationConfiguration.Validate(EscalationConfigJson);
}

internal static class SlaJson
{
    internal static string Container(string json, JsonValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != kind) throw new ArgumentException($"A JSON {kind} is required.", nameof(json));
            return document.RootElement.GetRawText();
        }
        catch (JsonException failure) { throw new ArgumentException("Valid JSON is required.", nameof(json), failure); }
    }
}
