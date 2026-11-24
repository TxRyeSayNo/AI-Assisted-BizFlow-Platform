using System.Globalization;
using System.Text.RegularExpressions;

namespace BizFlow.Application.AI;

public static class AgentTimeEvidence
{
    private static readonly Dictionary<string, int> Hours = new(StringComparer.OrdinalIgnoreCase)
    {
        ["không"] = 0, ["một"] = 1, ["hai"] = 2, ["ba"] = 3, ["bốn"] = 4, ["năm"] = 5, ["sáu"] = 6,
        ["bảy"] = 7, ["tám"] = 8, ["chín"] = 9, ["mười"] = 10, ["mười một"] = 11, ["mười hai"] = 12,
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6,
        ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12
    };
    private static readonly Regex Clock = new(@"(?<![\d:+-])(?<hour>[01]?\d|2[0-3]|" + string.Join("|", Hours.Keys.OrderByDescending(s => s.Length).Select(Regex.Escape)) +
        @")\s*(?:(?:[:hH])\s*(?<minute>[0-5]\d)?|giờ\s*(?<minute>[0-5]\d)?|am|pm|o'clock|時)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Only user turns are evidence. A model's previous summary or the server clock cannot
    // supply a meeting hour that the user never gave.
    public static string? Find(string instantText, string timeZone, IEnumerable<string> userTurns)
    {
        if (!DateTimeOffset.TryParse(instantText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant)) return null;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (TimeZoneNotFoundException) { return null; }
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        foreach (var turn in userTurns)
        {
            var matches = Clock.Matches(turn);
            if (matches.Count == 0) continue;
            var match = matches[^1];
            var hourText = match.Groups["hour"].Value;
            var hour = int.TryParse(hourText, CultureInfo.InvariantCulture, out var numericHour) ? numericHour : Hours[hourText];
            var minute = match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
            var tail = turn[match.Index..Math.Min(turn.Length, match.Index + match.Length + 12)].ToLowerInvariant();
            if (tail.Contains("rưỡi")) minute = 30;
            if (hour < 12 && (tail.Contains("chiều") || tail.Contains("tối") || tail.Contains("pm"))) hour += 12;
            if (hour == 12 && (tail.Contains("sáng") || tail.Contains("am"))) hour = 0;
            // The most recent explicit time is authoritative, including corrections such as 'not 9h, 10h'.
            return local.Hour == hour && local.Minute == minute ? turn : null;
        }
        return null;
    }
}
