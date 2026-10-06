using BizFlow.Domain.Sla;

namespace BizFlow.UnitTests;

public sealed class SlaSnapshotTests
{
    [Fact]
    public void Calendar_preserves_dictionary_fields_and_safe_structural_defaults()
    {
        var tenant = Guid.NewGuid(); var calendar = BusinessCalendar.Create(tenant);
        Assert.Equal(tenant, calendar.TenantId); Assert.NotEqual(Guid.Empty, calendar.Id);
        Assert.Equal("UTC", calendar.TimeZone); Assert.Equal("{}", calendar.WorkingHoursJson); Assert.Equal("[]", calendar.HolidaysJson);
        Assert.Equal("Asia/Ho_Chi_Minh", BusinessCalendar.Create(tenant, "Asia/Ho_Chi_Minh").TimeZone);
    }
    [Theory]
    [InlineData("", "{}", "[]")]
    [InlineData("not/a-timezone", "{}", "[]")]
    [InlineData("SE Asia Standard Time", "{}", "[]")]
    [InlineData("UTC", "[]", "[]")]
    [InlineData("UTC", "null", "[]")]
    [InlineData("UTC", "broken", "[]")]
    [InlineData("UTC", "{}", "{}")]
    [InlineData("UTC", "{}", "null")]
    public void Invalid_calendar_storage_is_rejected(string zone, string hours, string holidays) =>
        Assert.Throws<ArgumentException>(() => BusinessCalendar.Create(Guid.NewGuid(), zone, hours, holidays));

    [Fact]
    public void Snapshot_retains_references_and_thresholds_without_a_publication_state()
    {
        var profile = Guid.NewGuid(); var calendar = Guid.NewGuid();
        var version = SlaVersion.CreateSnapshot(profile, 2, 120, 90, calendar);
        Assert.NotEqual(Guid.Empty, version.Id); Assert.Equal(profile, version.SlaProfileId); Assert.Equal(calendar, version.CalendarId);
        Assert.Equal(2, version.VersionNo); Assert.Equal(120, version.TargetMinutes); Assert.Equal(90, version.WarningMinutes); Assert.Null(version.EscalationConfigJson);
    }
    [Theory]
    [InlineData(0, 60, 30)]
    [InlineData(1, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 60, -1)]
    [InlineData(1, 60, 60)]
    [InlineData(1, 60, 61)]
    public void Approved_A08_threshold_and_version_number_rules_are_enforced(int number, int target, int warning) =>
        Assert.ThrowsAny<ArgumentException>(() => SlaVersion.CreateSnapshot(Guid.NewGuid(), number, target, warning, Guid.NewGuid()));
    [Fact]
    public void Missing_ownership_and_non_object_escalation_storage_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => BusinessCalendar.Create(Guid.Empty));
        Assert.Throws<ArgumentException>(() => SlaVersion.CreateSnapshot(Guid.Empty, 1, 1, 0, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => SlaVersion.CreateSnapshot(Guid.NewGuid(), 1, 1, 0, Guid.Empty));
        foreach (var json in new[] { "[]", "null", "false", "broken" })
            Assert.Throws<ArgumentException>(() => SlaVersion.CreateSnapshot(Guid.NewGuid(), 1, 1, 0, Guid.NewGuid(), json));
        Assert.Empty(SlaVersion.CreateSnapshot(Guid.NewGuid(), 1, 1, 0, Guid.NewGuid(), "{\"levels\":[]}").EscalationRecipientIds());
    }
    [Fact]
    public void Weekly_hours_allow_split_shifts_midnight_boundaries_and_real_leap_dates()
    {
        var calendar = BusinessCalendar.Create(Guid.NewGuid(), "UTC",
            """{"monday":[{"start":"13:00","end":"17:30"},{"start":"08:00","end":"12:00"}],"friday":[{"start":"22:00","end":"24:00"}],"saturday":[{"start":"00:00","end":"02:00"}]}""", """["2028-02-29"]""");
        Assert.True(calendar.HasWorkingTime());
        Assert.False(BusinessCalendar.Create(Guid.NewGuid()).HasWorkingTime());
        Assert.False(BusinessCalendar.Create(Guid.NewGuid(), "UTC", """{"monday":[]}""").HasWorkingTime());
    }
    [Theory]
    [InlineData("{\"Monday\":[]}", "[]")]
    [InlineData("{\"monday\":{},\"tuesday\":[]}", "[]")]
    [InlineData("{\"monday\":[],\"monday\":[]}", "[]")]
    [InlineData("{\"monday\":[{\"start\":\"08:00\",\"end\":\"08:00\"}]}", "[]")]
    [InlineData("{\"monday\":[{\"start\":\"22:00\",\"end\":\"02:00\"}]}", "[]")]
    [InlineData("{\"monday\":[{\"start\":\"24:00\",\"end\":\"24:00\"}]}", "[]")]
    [InlineData("{\"monday\":[{\"start\":\"8:00\",\"end\":\"17:00\"}]}", "[]")]
    [InlineData("{\"monday\":[{\"start\":\"08:00\",\"end\":\"17:00\",\"extra\":true}]}", "[]")]
    [InlineData("{\"monday\":[{\"start\":\"08:00\",\"end\":\"12:00\"},{\"start\":\"11:59\",\"end\":\"17:30\"}]}", "[]")]
    [InlineData("{}", "[\"2026-02-29\"]")]
    [InlineData("{}", "[\"2028-02-29\",\"2028-02-29\"]")]
    [InlineData("{}", "[\"2026-1-01\"]")]
    [InlineData("{}", "[\"2026-01-01T00:00:00Z\"]")]
    [InlineData("{}", "[123]")]
    public void Calendar_contract_rejects_ambiguous_or_invalid_inputs(string hours, string holidays) =>
        Assert.Throws<ArgumentException>(() => BusinessCalendar.Create(Guid.NewGuid(), "UTC", hours, holidays));

    [Fact]
    public void Escalation_levels_can_notify_the_same_user_at_different_offsets()
    {
        var id = Guid.NewGuid();
        var json = System.Text.Json.JsonSerializer.Serialize(new { levels = new[] {
            new { level = 1, afterMinutes = 0, recipientUserIds = new[] { id } },
            new { level = 2, afterMinutes = 30, recipientUserIds = new[] { id } } } });
        var version = SlaVersion.CreateSnapshot(Guid.NewGuid(), 1, 60, 45, Guid.NewGuid(), json);
        Assert.Equal(id, Assert.Single(version.EscalationRecipientIds())); Assert.Equal(json, version.EscalationConfigJson);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"levels\":[],\"other\":true}")]
    [InlineData("{\"levels\":[],\"levels\":[]}")]
    [InlineData("{\"levels\":[{\"level\":2,\"afterMinutes\":0,\"recipientUserIds\":[\"00000000-0000-7000-8000-000000000001\"]}]}")]
    [InlineData("{\"levels\":[{\"level\":1,\"afterMinutes\":-1,\"recipientUserIds\":[\"00000000-0000-7000-8000-000000000001\"]}]}")]
    [InlineData("{\"levels\":[{\"level\":1,\"afterMinutes\":1.5,\"recipientUserIds\":[\"00000000-0000-7000-8000-000000000001\"]}]}")]
    [InlineData("{\"levels\":[{\"level\":1,\"afterMinutes\":0,\"recipientUserIds\":[]}]}")]
    [InlineData("{\"levels\":[{\"level\":1,\"afterMinutes\":0,\"recipientUserIds\":[\"00000000-0000-0000-0000-000000000000\"]}]}")]
    [InlineData("{\"levels\":[{\"level\":1,\"afterMinutes\":0,\"recipientUserIds\":[\"not-a-guid\"]}]}")]
    [InlineData("{\"levels\":[{\"level\":1,\"afterMinutes\":0,\"recipientUserIds\":[\"00000000-0000-7000-8000-000000000001\",\"00000000-0000-7000-8000-000000000001\"]}]}")]
    public void Escalation_schema_rejects_ambiguous_or_unsupported_data(string json) =>
        Assert.Throws<ArgumentException>(() => SlaVersion.CreateSnapshot(Guid.NewGuid(), 1, 60, 45, Guid.NewGuid(), json));
    [Fact]
    public void Escalation_offsets_must_strictly_increase()
    {
        foreach (var offset in new[] { 0, 10 })
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { levels = new[] {
                new { level = 1, afterMinutes = 10, recipientUserIds = new[] { Guid.NewGuid() } },
                new { level = 2, afterMinutes = offset, recipientUserIds = new[] { Guid.NewGuid() } } } });
            Assert.Throws<ArgumentException>(() => SlaVersion.CreateSnapshot(Guid.NewGuid(), 1, 60, 45, Guid.NewGuid(), json));
        }
    }
}
