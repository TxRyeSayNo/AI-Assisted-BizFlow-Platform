using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tasks;

public sealed class TaskProgressReport
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public short Percent { get; private set; }
    public string? Content { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    private TaskProgressReport() { }

    // Persistence factory, not a progress command. The use case must load the current
    // report under lock and enforce non-decreasing progress or audited correction reason.
    public static TaskProgressReport Create(Guid taskId, Guid authorId, short percent, string? content, DateTimeOffset now)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        return new()
        {
            Id = Guid.CreateVersion7(), TaskId = EntityRules.Id(taskId, nameof(taskId)),
            AuthorId = EntityRules.Id(authorId, nameof(authorId)), Percent = percent,
            Content = TaskEvidenceText.Normalize(content), SubmittedAt = now.ToUniversalTime()
        };
    }

    public static TaskProgressReport CreateFormalReport(Guid taskId, Guid authorId, short percent, string content, DateTimeOffset now)
    {
        var text = TaskEvidenceText.Normalize(content);
        if (text is null) throw new ArgumentException("Formal progress report content is required.", nameof(content));
        return Create(taskId, authorId, percent, text, now);
    }
}

public sealed class TaskResult
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string? Content { get; private set; }
    public int RevisionNo { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    private TaskResult() { }

    // Preserve Appendix C's nullable content. This alone is not sufficient evidence for
    // SUBMITTED: the application must validate required result/evidence and performer authority.
    public static TaskResult CreateSnapshot(Guid taskId, Guid authorId, string? content, int revisionNo, DateTimeOffset now)
    {
        if (revisionNo < 1) throw new ArgumentOutOfRangeException(nameof(revisionNo));
        return new()
        {
            Id = Guid.CreateVersion7(), TaskId = EntityRules.Id(taskId, nameof(taskId)),
            AuthorId = EntityRules.Id(authorId, nameof(authorId)), Content = TaskEvidenceText.Normalize(content),
            RevisionNo = revisionNo, SubmittedAt = now.ToUniversalTime()
        };
    }
}

internal static class TaskEvidenceText
{
    internal static string? Normalize(string? content)
    {
        if (content?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Evidence text contains unsupported control characters.", nameof(content));
        return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
    }
}
