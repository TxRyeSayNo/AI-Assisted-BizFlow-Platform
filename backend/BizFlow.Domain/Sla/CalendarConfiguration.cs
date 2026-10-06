using System.Globalization;
using System.Text.Json;

namespace BizFlow.Domain.Sla;

internal static class CalendarConfiguration
{
    private static readonly HashSet<string> Weekdays = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
    internal static void Validate(string hoursJson, string holidaysJson)
    {
        using var hours = JsonDocument.Parse(SlaJson.Container(hoursJson, JsonValueKind.Object));
        var seen = new HashSet<string>();
        foreach (var day in hours.RootElement.EnumerateObject())
        {
            if (!Weekdays.Contains(day.Name) || !seen.Add(day.Name) || day.Value.ValueKind != JsonValueKind.Array) throw Invalid();
            var intervals = new List<(int Start, int End)>();
            foreach (var interval in day.Value.EnumerateArray())
            {
                if (interval.ValueKind != JsonValueKind.Object) throw Invalid();
                var properties = interval.EnumerateObject().ToArray();
                if (properties.Length != 2 || !interval.TryGetProperty("start", out var start) || !interval.TryGetProperty("end", out var end)) throw Invalid();
                var from = Minute(start, false); var to = Minute(end, true);
                if (to <= from) throw Invalid();
                intervals.Add((from, to));
            }
            var ordered = intervals.OrderBy(i => i.Start).ToArray();
            for (var i = 1; i < ordered.Length; i++) if (ordered[i].Start < ordered[i - 1].End) throw Invalid();
        }
        using var holidays = JsonDocument.Parse(SlaJson.Container(holidaysJson, JsonValueKind.Array));
        var dates = new HashSet<DateOnly>();
        foreach (var item in holidays.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(item.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) || !dates.Add(date)) throw Invalid();
        }
    }
    internal static bool HasWorkingTime(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        return parsed.RootElement.EnumerateObject().Any(day => day.Value.GetArrayLength() > 0);
    }
    private static int Minute(JsonElement value, bool end)
    {
        if (value.ValueKind != JsonValueKind.String) throw Invalid();
        var text = value.GetString();
        if (end && text == "24:00") return 1440;
        if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) throw Invalid();
        return time.Hour * 60 + time.Minute;
    }
    private static ArgumentException Invalid() => new("Use lowercase weekdays, nonoverlapping same-day HH:mm intervals and unique YYYY-MM-DD holiday dates.");
}
