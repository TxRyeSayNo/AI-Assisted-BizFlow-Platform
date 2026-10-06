using System.Text.Json;

namespace BizFlow.Domain.Sla;

internal static class EscalationConfiguration
{
    internal static IReadOnlyList<Guid> Validate(string? json)
    {
        if (json is null) return [];
        using var parsed = JsonDocument.Parse(SlaJson.Container(json, JsonValueKind.Object));
        var root = parsed.RootElement;
        if (root.EnumerateObject().Count() != 1 || !root.TryGetProperty("levels", out var levels) || levels.ValueKind != JsonValueKind.Array) throw Invalid();
        var expectedLevel = 1; var previousOffset = -1; var recipients = new HashSet<Guid>();
        foreach (var level in levels.EnumerateArray())
        {
            if (level.ValueKind != JsonValueKind.Object || level.EnumerateObject().Count() != 3 ||
                !level.TryGetProperty("level", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var n) || n != expectedLevel ||
                !level.TryGetProperty("afterMinutes", out var offset) || offset.ValueKind != JsonValueKind.Number || !offset.TryGetInt32(out var minutes) || minutes <= previousOffset ||
                !level.TryGetProperty("recipientUserIds", out var users) || users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0) throw Invalid();
            var levelRecipients = new HashSet<Guid>();
            foreach (var user in users.EnumerateArray())
            {
                if (user.ValueKind != JsonValueKind.String || !Guid.TryParseExact(user.GetString(), "D", out var id) || id == Guid.Empty || !levelRecipients.Add(id)) throw Invalid();
                recipients.Add(id);
            }
            expectedLevel++; previousOffset = minutes;
        }
        return recipients.Order().ToArray();
    }
    private static ArgumentException Invalid() => new("Use ordered levels 1 through N, increasing nonnegative afterMinutes and nonempty unique recipient UUID lists.");
}
