using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

/// <summary>
/// The merged template/recurrence record (F8 + F9) and the private saved filter (F12).
/// </summary>
public sealed class TaskTemplateTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();

    // A Tuesday, 10:30 UTC.
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 30, 0, TimeSpan.Zero);

    private static TaskTemplate Template(int? offset = 3) =>
        TaskTemplate.Create(Tenant, " Báo cáo tuần ", " Làm báo cáo tuần ", " Tổng hợp số liệu ",
            "high", offset, Actor, Now);

    [Fact]
    public void A_template_trims_its_fields_and_starts_without_a_schedule()
    {
        var template = Template();

        Assert.Equal("Báo cáo tuần", template.Name);
        Assert.Equal("BÁO CÁO TUẦN", template.NormalizedName);
        Assert.Equal("Làm báo cáo tuần", template.TitleTemplate);
        Assert.Equal("Tổng hợp số liệu", template.Description);
        Assert.Equal("HIGH", template.Priority);
        Assert.Equal(3, template.DeadlineOffsetDays);
        Assert.False(template.Repeats);
        Assert.Null(template.NextRunAt);
        // Day granularity, so two calls on the same day produce an identical task.
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 23, 59, 0, TimeSpan.Zero), template.DeadlineFor(Now));
        Assert.Equal(template.DeadlineFor(Now), template.DeadlineFor(Now.AddHours(2)));
        Assert.Null(Template(null).DeadlineFor(Now));
    }

    [Fact]
    public void A_deadline_offset_and_a_blank_name_are_both_refused()
    {
        Assert.Throws<ArgumentException>(() => TaskTemplate.Create(Tenant, "   ", "T", null, null, 1, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskTemplate.Create(Tenant, "N", "   ", null, null, 1, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskTemplate.Create(Tenant, "N", "T", null, null, -1, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskTemplate.Create(Tenant, "N", "T", null, null,
            TaskTemplate.MaxDeadlineOffsetDays + 1, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskTemplate.Create(Tenant, "N", "Tab\u0001here", null, null, 1, Actor, Now));
    }

    [Fact]
    public void Editing_reports_whether_anything_actually_changed()
    {
        var template = Template();

        Assert.False(template.Edit(" Báo cáo tuần ", "Làm báo cáo tuần", "Tổng hợp số liệu", "HIGH", 3, Now));
        Assert.Null(template.UpdatedAt);
        Assert.True(template.Edit("Báo cáo tháng", "Làm báo cáo tháng", null, null, null, Now.AddHours(1)));
        Assert.Equal("BÁO CÁO THÁNG", template.NormalizedName);
        Assert.Null(template.Description);
        Assert.Null(template.DeadlineOffsetDays);
        Assert.Equal(Now.AddHours(1), template.UpdatedAt);
    }

    [Fact]
    public void A_daily_schedule_lands_on_the_next_hour_slot()
    {
        var template = Template();
        template.Schedule("daily", null, 9, Now);

        Assert.Equal("DAILY", template.RecurrenceFrequency);
        Assert.Null(template.RecurrenceDay);
        Assert.Equal(9, template.RecurrenceHourUtc);
        // 09:00 has passed at 10:30, so the next slot is tomorrow.
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero), template.NextRunAt);

        template.Schedule("DAILY", 5, 23, Now);
        // A daily schedule has no day, whatever the caller passes.
        Assert.Null(template.RecurrenceDay);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 0, 0, TimeSpan.Zero), template.NextRunAt);
    }

    [Fact]
    public void A_weekly_schedule_counts_days_from_monday_and_a_monthly_one_is_capped_at_28()
    {
        var template = Template();

        // Now is Tuesday (2). Friday is 5, so three days ahead.
        template.Schedule("WEEKLY", 5, 8, Now);
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero), template.NextRunAt);
        // Today at an hour already past means next week, not today.
        template.Schedule("WEEKLY", 2, 8, Now);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero), template.NextRunAt);

        template.Schedule("MONTHLY", 28, 7, Now);
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 7, 0, 0, TimeSpan.Zero), template.NextRunAt);
        // A day already past this month rolls to the next.
        template.Schedule("MONTHLY", 1, 7, Now);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.Zero), template.NextRunAt);
    }

    [Fact]
    public void An_impossible_schedule_is_refused()
    {
        var template = Template();

        Assert.Throws<ArgumentException>(() => template.Schedule("YEARLY", 1, 0, Now));
        Assert.Throws<ArgumentException>(() => template.Schedule("WEEKLY", 8, 0, Now));
        Assert.Throws<ArgumentException>(() => template.Schedule("WEEKLY", null, 0, Now));
        Assert.Throws<ArgumentException>(() => template.Schedule("MONTHLY", 29, 0, Now));
        Assert.Throws<ArgumentException>(() => template.Schedule("DAILY", null, 24, Now));
        Assert.Throws<ArgumentException>(() => template.Schedule("DAILY", null, -1, Now));
        // None of that left a half-applied schedule behind.
        Assert.False(template.Repeats);
        Assert.Null(template.NextRunAt);
    }

    [Fact]
    public void Clearing_a_schedule_stops_it_firing_and_a_run_steps_on_from_now()
    {
        var template = Template();
        template.Schedule("DAILY", null, 9, Now);

        var late = Now.AddDays(3);
        template.RecordRun(late);
        Assert.Equal(late, template.LastRunAt);
        // Stepping from "now" rather than from the missed slot means no backlog after an outage.
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 9, 0, 0, TimeSpan.Zero), template.NextRunAt);

        template.Schedule(null, null, null, late);
        Assert.False(template.Repeats);
        Assert.Null(template.NextRunAt);
        Assert.Throws<InvalidOperationException>(() => template.RecordRun(late));
    }

    [Fact]
    public void Archiving_stops_the_schedule_and_restoring_recomputes_it()
    {
        var template = Template();
        template.Schedule("DAILY", null, 9, Now);

        Assert.True(template.Archive(Now));
        Assert.Null(template.NextRunAt);
        Assert.True(template.Repeats);
        Assert.False(template.Archive(Now));

        Assert.True(template.Restore(Now));
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero), template.NextRunAt);
        Assert.False(template.Restore(Now));

        // A template with no schedule stays dormant after a restore.
        var plain = Template();
        plain.Archive(Now);
        plain.Restore(Now);
        Assert.Null(plain.NextRunAt);
    }

    [Fact]
    public void A_saved_filter_belongs_to_one_person_and_one_list()
    {
        var user = Guid.CreateVersion7();
        var filter = SavedFilter.Create(Tenant, user, "task", " Việc gấp ", "?priority=HIGH&status=ASSIGNED", Now);

        Assert.Equal("TASK", filter.Scope);
        Assert.Equal("Việc gấp", filter.Name);
        Assert.Equal("VIỆC GẤP", filter.NormalizedName);
        // The stored query never keeps the leading "?".
        Assert.Equal("priority=HIGH&status=ASSIGNED", filter.Query);
        Assert.Equal(user, filter.UserId);

        Assert.Throws<ArgumentException>(() => SavedFilter.Create(Tenant, user, "PROJECT", "N", "", Now));
        Assert.Throws<ArgumentException>(() => SavedFilter.Create(Tenant, user, "TASK", "  ", "", Now));
        Assert.Throws<ArgumentException>(() => SavedFilter.Create(Tenant, user, "TASK", "N", "a=\u0001b", Now));
        Assert.Throws<ArgumentException>(() => SavedFilter.Create(Tenant, user, "TASK", "N",
            new string('a', SavedFilter.MaxQueryLength + 1), Now));
    }

    [Fact]
    public void Deleting_a_saved_filter_is_soft_and_reusing_the_row_renames_it()
    {
        var filter = SavedFilter.Create(Tenant, Guid.CreateVersion7(), "TASK", "Việc gấp", "priority=HIGH", Now);

        Assert.False(filter.Edit("Việc gấp", "priority=HIGH", Now));
        Assert.True(filter.Edit("Việc rất gấp", "priority=CRITICAL", Now));
        Assert.Equal("priority=CRITICAL", filter.Query);

        Assert.True(filter.Remove(Now));
        Assert.NotNull(filter.RemovedAt);
        Assert.False(filter.Remove(Now));

        Assert.True(filter.Restore("Tên mới", "status=DRAFT", Now));
        Assert.Null(filter.RemovedAt);
        Assert.Equal("TÊN MỚI", filter.NormalizedName);
        Assert.Equal("status=DRAFT", filter.Query);
        Assert.False(filter.Restore("Tên mới", "status=DRAFT", Now));
    }
}
