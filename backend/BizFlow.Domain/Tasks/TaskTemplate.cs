using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tasks;

/// <summary>
/// A reusable task template (F9), optionally repeating on a schedule (F8).
///
/// F8 and F9 were asked for separately but are one thing: "a task you create again and again".
/// Splitting them would have meant two tables holding the same title, description, priority and
/// deadline offset, kept in step by hand. So a template is one row, and a recurrence is three
/// optional fields on it. One schedule per template is the deliberate limit of that choice.
///
/// Retiring a template archives it, like a label: the tasks it already produced are untouched.
/// </summary>
public sealed class TaskTemplate
{
    public const int MaxNameLength = 120;
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;

    /// <summary>Furthest ahead a template may push a generated task's deadline.</summary>
    public const int MaxDeadlineOffsetDays = 365;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>What the template is called in the catalogue.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Upper-case name used for the per-tenant uniqueness check.</summary>
    public string NormalizedName { get; private set; } = "";

    /// <summary>The title given to each task created from this template.</summary>
    public string TitleTemplate { get; private set; } = "";

    public string? Description { get; private set; }

    /// <summary>A task priority code, or null to let the task default apply.</summary>
    public string? Priority { get; private set; }

    /// <summary>Days from creation to the generated task's deadline, or null for no deadline.</summary>
    public int? DeadlineOffsetDays { get; private set; }

    /// <summary>DAILY, WEEKLY or MONTHLY; null means the template never fires on its own.</summary>
    public string? RecurrenceFrequency { get; private set; }

    /// <summary>Day of the week (1-7, Monday first) or of the month (1-28); null when daily.</summary>
    public int? RecurrenceDay { get; private set; }

    /// <summary>Hour of day in UTC, 0-23.</summary>
    public int? RecurrenceHourUtc { get; private set; }

    /// <summary>When the schedule is next due. Null whenever there is no recurrence.</summary>
    public DateTimeOffset? NextRunAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool Repeats => RecurrenceFrequency is not null;

    private TaskTemplate() { }

    public static TaskTemplate Create(Guid tenantId, string name, string titleTemplate, string? description,
        string? priority, int? deadlineOffsetDays, Guid createdBy, DateTimeOffset now)
    {
        var cleanName = PlainLine(name, MaxNameLength, nameof(name));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            Name = cleanName,
            NormalizedName = Normalize(cleanName),
            TitleTemplate = PlainLine(titleTemplate, MaxTitleLength, nameof(titleTemplate)),
            Description = Clean(description, MaxDescriptionLength, nameof(description)),
            Priority = Clean(priority, 32, nameof(priority))?.ToUpperInvariant(),
            DeadlineOffsetDays = Offset(deadlineOffsetDays),
            CreatedBy = EntityRules.Id(createdBy, nameof(createdBy)),
            CreatedAt = now
        };
    }

    public bool Edit(string name, string titleTemplate, string? description, string? priority,
        int? deadlineOffsetDays, DateTimeOffset now)
    {
        var cleanName = PlainLine(name, MaxNameLength, nameof(name));
        var cleanTitle = PlainLine(titleTemplate, MaxTitleLength, nameof(titleTemplate));
        var cleanDescription = Clean(description, MaxDescriptionLength, nameof(description));
        var cleanPriority = Clean(priority, 32, nameof(priority))?.ToUpperInvariant();
        var offset = Offset(deadlineOffsetDays);
        if (cleanName == Name && cleanTitle == TitleTemplate && cleanDescription == Description &&
            cleanPriority == Priority && offset == DeadlineOffsetDays) return false;
        Name = cleanName;
        NormalizedName = Normalize(cleanName);
        TitleTemplate = cleanTitle;
        Description = cleanDescription;
        Priority = cleanPriority;
        DeadlineOffsetDays = offset;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Turns the schedule on, changes it, or — with a null frequency — turns it off.</summary>
    public void Schedule(string? frequency, int? day, int? hourUtc, DateTimeOffset now)
    {
        if (frequency is null)
        {
            RecurrenceFrequency = null;
            RecurrenceDay = null;
            RecurrenceHourUtc = null;
            NextRunAt = null;
            UpdatedAt = now;
            return;
        }
        var clean = frequency.Trim().ToUpperInvariant();
        if (clean is not ("DAILY" or "WEEKLY" or "MONTHLY"))
            throw new ArgumentException("A recurrence must be DAILY, WEEKLY or MONTHLY.", nameof(frequency));
        var hour = hourUtc ?? 0;
        if (hour is < 0 or > 23)
            throw new ArgumentException("A recurrence hour must be between 0 and 23.", nameof(hourUtc));
        // A monthly day is capped at 28 so every month has it; a weekly day is Monday-first.
        var limit = clean == "WEEKLY" ? 7 : 28;
        if (clean == "DAILY") day = null;
        else if (day is null || day < 1 || day > limit)
            throw new ArgumentException($"A {clean.ToLowerInvariant()} recurrence needs a day from 1 to {limit}.",
                nameof(day));

        RecurrenceFrequency = clean;
        RecurrenceDay = day;
        RecurrenceHourUtc = hour;
        NextRunAt = NextOccurrence(clean, day, hour, now);
        UpdatedAt = now;
    }

    /// <summary>
    /// Records that the schedule fired and moves it on. Called only by the recurring job, which is
    /// why it refuses to run on a template with no schedule.
    /// </summary>
    public void RecordRun(DateTimeOffset now)
    {
        if (RecurrenceFrequency is null)
            throw new InvalidOperationException("A template without a recurrence does not run.");
        LastRunAt = now;
        // Step from "now" rather than from the missed slot, so a long outage cannot queue a backlog
        // of past occurrences.
        NextRunAt = NextOccurrence(RecurrenceFrequency, RecurrenceDay, RecurrenceHourUtc ?? 0, now);
        UpdatedAt = now;
    }

    public bool Archive(DateTimeOffset now)
    {
        if (ArchivedAt is not null) return false;
        ArchivedAt = now;
        // An archived template must stop firing; re-enabling is an explicit act.
        NextRunAt = null;
        UpdatedAt = now;
        return true;
    }

    public bool Restore(DateTimeOffset now)
    {
        if (ArchivedAt is null) return false;
        ArchivedAt = null;
        if (RecurrenceFrequency is not null)
            NextRunAt = NextOccurrence(RecurrenceFrequency, RecurrenceDay, RecurrenceHourUtc ?? 0, now);
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// The deadline a task created at <paramref name="now"/> would carry: the end of the day that
    /// many days ahead, in UTC.
    ///
    /// Day granularity rather than "now plus N days" is deliberate. An offset expressed in days
    /// means a day, not a moment, and — just as importantly — it makes the generated task identical
    /// for every call on the same day. The create command fingerprints its input to honour an
    /// Idempotency-Key, so a deadline that moved with the clock would make an honest retry look
    /// like a different request and be refused as a conflict.
    /// </summary>
    public DateTimeOffset? DeadlineFor(DateTimeOffset now)
    {
        if (DeadlineOffsetDays is not { } days) return null;
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 23, 59, 0, TimeSpan.Zero).AddDays(days);
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    /// <summary>The first slot strictly after <paramref name="now"/>.</summary>
    public static DateTimeOffset NextOccurrence(string frequency, int? day, int hourUtc, DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        switch (frequency)
        {
            case "DAILY":
            {
                var candidate = new DateTimeOffset(utc.Year, utc.Month, utc.Day, hourUtc, 0, 0, TimeSpan.Zero);
                return candidate > utc ? candidate : candidate.AddDays(1);
            }
            case "WEEKLY":
            {
                // DayOfWeek has Sunday at 0; the stored day is 1 = Monday.
                var today = (int)utc.DayOfWeek == 0 ? 7 : (int)utc.DayOfWeek;
                var ahead = ((day!.Value - today) + 7) % 7;
                var candidate = new DateTimeOffset(utc.Year, utc.Month, utc.Day, hourUtc, 0, 0, TimeSpan.Zero)
                    .AddDays(ahead);
                return candidate > utc ? candidate : candidate.AddDays(7);
            }
            default:
            {
                var candidate = new DateTimeOffset(utc.Year, utc.Month, day!.Value, hourUtc, 0, 0, TimeSpan.Zero);
                return candidate > utc ? candidate : candidate.AddMonths(1);
            }
        }
    }

    private static int? Offset(int? days) => days switch
    {
        null => null,
        < 0 or > MaxDeadlineOffsetDays => throw new ArgumentException(
            $"A deadline offset must be between 0 and {MaxDeadlineOffsetDays} days.", nameof(days)),
        _ => days
    };

    /// <summary>
    /// A required single-line field: trimmed, bounded, and plain text. A name or a title ends up in
    /// a task title, so a stray control character has no business surviving this far.
    /// </summary>
    private static string PlainLine(string value, int max, string name)
    {
        var text = EntityRules.Text(value, max, name);
        if (text.Any(char.IsControl)) throw new ArgumentException($"{name} must be plain text.", name);
        return text;
    }

    private static string? Clean(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > max) throw new ArgumentException($"{name} is too long.", name);
        if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException($"{name} must be plain text.", name);
        return text;
    }
}
