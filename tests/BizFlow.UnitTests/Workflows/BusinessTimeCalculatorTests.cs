using BizFlow.Domain.Sla;

namespace BizFlow.UnitTests.Workflows;

public sealed class BusinessTimeCalculatorTests
{
    private const string Hours = """{"monday":[{"start":"09:00","end":"12:00"},{"start":"13:00","end":"17:00"}],"tuesday":[{"start":"09:00","end":"17:00"}],"wednesday":[{"start":"09:00","end":"17:00"}],"thursday":[{"start":"09:00","end":"17:00"}],"friday":[{"start":"09:00","end":"17:00"}]}""";
    [Theory]
    [InlineData("[]", "2026-11-30T10:00:00+07:00")]
    [InlineData("[\"2026-11-30\"]", "2026-12-01T10:00:00+07:00")]
    public void Skips_weekends_and_local_holidays(string holidays, string expected)
    {
        var calendar = BusinessCalendar.Create(Guid.NewGuid(), "Asia/Ho_Chi_Minh", Hours, holidays);
        Assert.Equal(DateTimeOffset.Parse(expected).ToUniversalTime(), BusinessTimeCalculator.AddMinutes(calendar, DateTimeOffset.Parse("2026-11-27T16:00:00+07:00"), 120));
    }
    [Fact]
    public void Skips_lunch_break_and_honors_absolute_instants()
    {
        var calendar = BusinessCalendar.Create(Guid.NewGuid(), "Asia/Ho_Chi_Minh", Hours);
        Assert.Equal(DateTimeOffset.Parse("2026-11-30T13:30:00+07:00").ToUniversalTime(),
            BusinessTimeCalculator.AddMinutes(calendar, DateTimeOffset.Parse("2026-11-30T11:30:00+07:00"), 60));
    }
    [Fact]
    public void Rejects_calendars_without_working_hours() => Assert.Throws<ArgumentException>(() =>
        BusinessTimeCalculator.AddMinutes(BusinessCalendar.Create(Guid.NewGuid()), DateTimeOffset.UtcNow, 60));
}
