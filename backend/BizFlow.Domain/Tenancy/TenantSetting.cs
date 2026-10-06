using System.Text.Json;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tenancy;

// Approved architecture A-03. Storage validation is not permission to execute a tool,
// accept an upload or change an SLA: each consuming Application use case still validates.
public sealed class TenantSetting
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = "";
    public string ValueJson { get; private set; } = "";
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private TenantSetting() { }

    public static TenantSetting Create(Guid tenantId, string key, string valueJson, Guid actorId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        Key = key, ValueJson = Validate(key, valueJson), UpdatedBy = EntityRules.Id(actorId, nameof(actorId)), UpdatedAt = now.ToUniversalTime()
    };

    public void SetValue(Guid tenantId, string valueJson, Guid actorId, DateTimeOffset now)
    {
        if (tenantId != TenantId) throw new InvalidOperationException("The setting belongs to a different tenant.");
        var actor = EntityRules.Id(actorId, nameof(actorId));
        var value = Validate(Key, valueJson);
        ValueJson = value; UpdatedBy = actor; UpdatedAt = now.ToUniversalTime();
    }

    private static string Validate(string key, string valueJson)
    {
        ArgumentNullException.ThrowIfNull(valueJson);
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var value = document.RootElement;
            var valid = key switch
            {
                "ai.enabled" or "ai.auto_action_enabled" or "sla.pause_on_waiting_for_information" or
                "request.generic_service_allowed" or "notification.email_enabled" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "ai.auto_action_tools" or "attachment.allowed_content_types" => value.ValueKind == JsonValueKind.Array &&
                    value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())),
                "ai.confidence_threshold" => Number(value, 0, 1, false),
                "ai.monthly_call_limit" => Number(value, 0, int.MaxValue, true),
                "attachment.max_size_bytes" => Number(value, 0, 524288000, true),
                "report.max_range_days" => Number(value, 1, int.MaxValue, true),
                _ => false
            };
            if (!valid) throw new ArgumentException("Unknown tenant policy key or invalid typed value.", nameof(valueJson));
            return value.GetRawText();
        }
        catch (JsonException) { throw new ArgumentException("The policy value must be valid JSON.", nameof(valueJson)); }
    }

    private static bool Number(JsonElement value, decimal minimum, decimal maximum, bool integral) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) &&
        number >= minimum && number <= maximum && (!integral || decimal.Truncate(number) == number);
}
