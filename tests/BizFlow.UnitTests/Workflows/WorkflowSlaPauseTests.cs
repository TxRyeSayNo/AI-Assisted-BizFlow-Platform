using BizFlow.Domain.Sla;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowSlaPauseTests
{
    private static WorkflowStepExecution Step(DateTimeOffset start,DateTimeOffset due)
    {
        var instance=WorkflowInstance.Start(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"{}",start);
        return WorkflowStepExecution.Open(instance,"a","Duyệt","APPROVAL","SINGLE",[Guid.NewGuid()],start,due);
    }
    [Fact]
    public void Resume_excludes_only_working_minutes_of_the_pause_and_keeps_the_calendar_snapshot()
    {
        var start=DateTimeOffset.Parse("2026-11-27T10:00:00+07:00");var due=start.AddHours(6);
        var step=Step(start,due);var actor=Guid.NewGuid();
        var calendar=BusinessCalendar.Create(step.TenantId,"Asia/Ho_Chi_Minh","""{"friday":[{"start":"09:00","end":"17:00"}],"monday":[{"start":"09:00","end":"17:00"}],"tuesday":[{"start":"09:00","end":"17:00"}]}""","[\"2026-11-30\"]");
        var pause=WorkflowSlaPause.Begin(step,actor,"Chờ hồ sơ",start.AddHours(5),calendar);
        Assert.Throws<InvalidOperationException>(()=>step.Complete("APPROVE",start.AddHours(6)));
        pause.Resume(step,actor,DateTimeOffset.Parse("2026-12-01T10:00:00+07:00"));
        Assert.Equal(DateTimeOffset.Parse("2026-12-01T11:00:00+07:00").ToUniversalTime(),step.DueAt);
        Assert.Equal(due,pause.DueAtBefore);Assert.Null(step.PausedAt);Assert.NotNull(pause.CalendarSnapshotJson);
        Assert.Throws<InvalidOperationException>(()=>pause.Resume(step,actor,DateTimeOffset.Parse("2026-12-01T11:00:00+07:00")));
    }
    [Fact]
    public void Pausing_an_already_late_step_preserves_the_elapsed_breach_after_resume()
    {
        var start=DateTimeOffset.Parse("2026-11-25T09:00:00Z");var step=Step(start,start.AddHours(1));var actor=Guid.NewGuid();
        var pause=WorkflowSlaPause.Begin(step,actor,"Chờ bổ sung",start.AddHours(2),null);
        pause.Resume(step,actor,start.AddHours(5));
        Assert.Equal(start.AddHours(4),step.DueAt); // It remains one hour late; the pause cannot erase a breach.
        Assert.Equal(start.AddHours(1),pause.DueAtBefore);
    }
    [Fact]
    public void Business_time_measurement_skips_breaks_and_retains_fractional_minutes()
    {
        var calendar=BusinessCalendar.Create(Guid.NewGuid(),"UTC","""{"wednesday":[{"start":"09:00","end":"12:00"},{"start":"13:00","end":"17:00"}]}""");
        var from=DateTimeOffset.Parse("2026-11-25T11:59:30Z");var to=DateTimeOffset.Parse("2026-11-25T13:00:30Z");
        Assert.Equal(1,BusinessTimeCalculator.WorkingMinutesBetween(calendar,from,to));
        Assert.Equal(to,BusinessTimeCalculator.AddWorkingMinutes(calendar,from,1));
        Assert.Equal(-1,BusinessTimeCalculator.WorkingMinutesBetween(calendar,to,from));
    }
    [Fact]
    public void Adjacent_intervals_during_a_clock_fallback_do_not_count_the_repeated_hour_twice()
    {
        var calendar=BusinessCalendar.Create(Guid.NewGuid(),"America/New_York","""{"sunday":[{"start":"00:00","end":"01:30"},{"start":"01:30","end":"03:00"}]}""");
        var from=DateTimeOffset.Parse("2026-11-01T00:00:00-04:00");var to=DateTimeOffset.Parse("2026-11-01T03:00:00-05:00");
        Assert.Equal(240,BusinessTimeCalculator.WorkingMinutesBetween(calendar,from,to));
        Assert.Equal(to.ToUniversalTime(),BusinessTimeCalculator.AddWorkingMinutes(calendar,from,240));
    }
}
