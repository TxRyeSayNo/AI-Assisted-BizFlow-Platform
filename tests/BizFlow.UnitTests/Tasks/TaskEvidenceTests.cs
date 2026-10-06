using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskEvidenceTests
{
    [Fact]
    public void Evidence_factories_preserve_fields_utc_and_distinct_revisions()
    {
        var task = Guid.NewGuid(); var author = Guid.NewGuid(); var now = new DateTimeOffset(2026, 10, 4, 15, 0, 0, TimeSpan.FromHours(7));
        var progress = TaskProgressReport.Create(task, author, 50, "  <b>Literal</b>\nDetails ", now);
        Assert.Equal(task, progress.TaskId); Assert.Equal(author, progress.AuthorId); Assert.Equal(50, progress.Percent);
        Assert.Equal("<b>Literal</b>\nDetails", progress.Content); Assert.Equal(now.ToUniversalTime(), progress.SubmittedAt);
        Assert.Null(TaskProgressReport.Create(task, author, 0, null, now).Content);
        var formal = TaskProgressReport.CreateFormalReport(task, author, 75, "Report", now);
        Assert.Equal("Report", formal.Content); Assert.NotEqual(progress.Id, formal.Id);
        var first = TaskResult.CreateSnapshot(task, author, "First", 1, now);
        var second = TaskResult.CreateSnapshot(task, author, "Second", 2, now.AddDays(1));
        Assert.NotEqual(first.Id, second.Id); Assert.Equal("First", first.Content); Assert.Equal(1, first.RevisionNo);
        Assert.Equal(task, first.TaskId); Assert.Equal(author, first.AuthorId); Assert.Equal(now.ToUniversalTime(), first.SubmittedAt);
        Assert.Null(TaskResult.CreateSnapshot(task, author, null, 3, now).Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    public void Formal_reports_require_content_even_though_ordinary_progress_notes_are_optional(string? content)
    {
        Assert.Throws<ArgumentException>(() => TaskProgressReport.CreateFormalReport(Guid.NewGuid(), Guid.NewGuid(), 50, content!, DateTimeOffset.UtcNow));
        Assert.Null(TaskProgressReport.Create(Guid.NewGuid(), Guid.NewGuid(), 50, content, DateTimeOffset.UtcNow).Content);
    }

    [Fact]
    public void Invalid_percent_revision_ids_and_control_characters_are_rejected()
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        foreach (var percent in new short[] { -1, 101, short.MaxValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => TaskProgressReport.Create(id, id, percent, null, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskResult.CreateSnapshot(id, id, "Result", 0, now));
        Assert.Throws<ArgumentException>(() => TaskProgressReport.Create(Guid.Empty, id, 0, null, now));
        Assert.Throws<ArgumentException>(() => TaskProgressReport.Create(id, Guid.Empty, 0, null, now));
        Assert.Throws<ArgumentException>(() => TaskResult.CreateSnapshot(Guid.Empty, id, null, 1, now));
        Assert.Throws<ArgumentException>(() => TaskResult.CreateSnapshot(id, Guid.Empty, null, 1, now));
        Assert.Throws<ArgumentException>(() => TaskProgressReport.Create(id, id, 0, "Bad\u0001text", now));
        Assert.Throws<ArgumentException>(() => TaskResult.CreateSnapshot(id, id, "Bad\u007ftext", 1, now));
    }
}
