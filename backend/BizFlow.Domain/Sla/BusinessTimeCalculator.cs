using System.Globalization;
using System.Text.Json;

namespace BizFlow.Domain.Sla;

/// <summary>Consumes minutes only inside calendar intervals; dates and holidays are local to the calendar.</summary>
public static class BusinessTimeCalculator
{
    public static DateTimeOffset AddMinutes(BusinessCalendar calendar, DateTimeOffset start, int minutes)
    {
        if (minutes is < 0 or > 525600) throw new ArgumentOutOfRangeException(nameof(minutes));
        return AddWorkingMinutes(calendar, start, minutes);
    }

    public static DateTimeOffset AddWorkingMinutes(BusinessCalendar calendar, DateTimeOffset start, double minutes)
    {
        if (!double.IsFinite(minutes) || minutes is < 0 or > 5256000) throw new ArgumentOutOfRangeException(nameof(minutes));
        if (minutes == 0) return start.ToUniversalTime();
        if (!calendar.HasWorkingTime()) throw new ArgumentException("Lịch không có giờ làm việc.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZone);
        using var hours = JsonDocument.Parse(calendar.WorkingHoursJson);
        using var days = JsonDocument.Parse(calendar.HolidaysJson);
        var holidays = days.RootElement.EnumerateArray().Select(x => x.GetString()).ToHashSet();
        var date = TimeZoneInfo.ConvertTime(start, zone).Date;
        var remaining = (double)minutes;
        var coveredThrough = start.ToUniversalTime();
        for (var day = 0; day < 3660; day++, date = date.AddDays(1))
        {
            if (holidays.Contains(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) ||
                !hours.RootElement.TryGetProperty(date.DayOfWeek.ToString().ToLowerInvariant(), out var intervals)) continue;
            var ordered = intervals.EnumerateArray().OrderBy(x => x.GetProperty("start").GetString(), StringComparer.Ordinal);
            foreach (var interval in ordered)
            {
                var from = Boundary(date, interval.GetProperty("start").GetString()!, zone, false);
                var to = Boundary(date, interval.GetProperty("end").GetString()!, zone, true);
                var effectiveStart = from > coveredThrough ? from : coveredThrough;
                if (to <= effectiveStart) continue;
                var available = (to - effectiveStart).TotalMinutes;
                if (available >= remaining) return effectiveStart.AddMinutes(remaining);
                remaining -= available;
                coveredThrough = to;
            }
        }
        throw new ArgumentException("Không tìm được đủ giờ làm việc trong thời gian cho phép.");
    }

    public static double WorkingMinutesBetween(BusinessCalendar calendar, DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from) return -WorkingMinutesBetween(calendar, to, from);
        if (to - from > TimeSpan.FromDays(3660)) throw new ArgumentOutOfRangeException(nameof(to));
        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZone);
        using var hours = JsonDocument.Parse(calendar.WorkingHoursJson);
        using var days = JsonDocument.Parse(calendar.HolidaysJson);
        var holidays = days.RootElement.EnumerateArray().Select(x => x.GetString()).ToHashSet();
        var date = TimeZoneInfo.ConvertTime(from, zone).Date;
        var endDate = TimeZoneInfo.ConvertTime(to, zone).Date;
        double total = 0;
        var coveredThrough = from;
        for (; date <= endDate; date = date.AddDays(1))
        {
            if (holidays.Contains(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) ||
                !hours.RootElement.TryGetProperty(date.DayOfWeek.ToString().ToLowerInvariant(), out var intervals)) continue;
            foreach (var interval in intervals.EnumerateArray().OrderBy(i => i.GetProperty("start").GetString(), StringComparer.Ordinal))
            {
                var start = Boundary(date, interval.GetProperty("start").GetString()!, zone, false);
                var end = Boundary(date, interval.GetProperty("end").GetString()!, zone, true);
                if (start < coveredThrough) start = coveredThrough;
                if (end > to) end = to;
                if (end > start) { total += (end - start).TotalMinutes; coveredThrough = end; }
            }
        }
        return total;
    }

    private static DateTimeOffset Boundary(DateTime date, string clock, TimeZoneInfo zone, bool end)
    {
        var local = DateTime.SpecifyKind(clock == "24:00" ? date.AddDays(1) : date.Add(TimeSpan.ParseExact(clock, @"hh\:mm", CultureInfo.InvariantCulture)), DateTimeKind.Unspecified);
        // A clock jump can remove a local boundary. Move to the first existing minute.
        for (var i = 0; zone.IsInvalidTime(local) && i < 180; i++) local = local.AddMinutes(1);
        if (zone.IsInvalidTime(local)) throw new ArgumentException("Giờ làm việc không hợp lệ tại múi giờ đã chọn.");
        var offset = zone.IsAmbiguousTime(local) ? (end ? zone.GetAmbiguousTimeOffsets(local).Min() : zone.GetAmbiguousTimeOffsets(local).Max()) : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
