using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tasks;

/// <summary>
/// One person's saved list filter (F12).
///
/// A saved filter is private to the person who saved it: it is a convenience, not shared
/// configuration, so nothing here is readable by a colleague even inside the same workspace. The
/// query itself is stored as the plain query string the client would otherwise type, because the
/// list endpoints remain the only thing that interprets it — and they re-validate every value and
/// re-apply the read policy on each call, so a stale or hand-edited filter can never widen what
/// its owner is allowed to see.
///
/// Deleting is a soft change, like the platform's other collaboration records.
/// </summary>
public sealed class SavedFilter
{
    public const int MaxNameLength = 80;
    public const int MaxQueryLength = 1000;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>The only person who can read or change this filter.</summary>
    public Guid UserId { get; private set; }

    /// <summary>TASK or REQUEST: which list the filter belongs to.</summary>
    public string Scope { get; private set; } = "";

    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";

    /// <summary>The query string, without a leading "?", exactly as the list endpoint takes it.</summary>
    public string Query { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }

    private SavedFilter() { }

    public static SavedFilter Create(Guid tenantId, Guid userId, string scope, string name, string query,
        DateTimeOffset now)
    {
        var cleanName = EntityRules.Text(name, MaxNameLength, nameof(name));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            Scope = CleanScope(scope),
            Name = cleanName,
            NormalizedName = Normalize(cleanName),
            Query = CleanQuery(query),
            CreatedAt = now
        };
    }

    public bool Edit(string name, string query, DateTimeOffset now)
    {
        var cleanName = EntityRules.Text(name, MaxNameLength, nameof(name));
        var cleanQuery = CleanQuery(query);
        if (cleanName == Name && cleanQuery == Query) return false;
        Name = cleanName;
        NormalizedName = Normalize(cleanName);
        Query = cleanQuery;
        UpdatedAt = now;
        return true;
    }

    public bool Remove(DateTimeOffset now)
    {
        if (RemovedAt is not null) return false;
        RemovedAt = now;
        UpdatedAt = now;
        return true;
    }

    public bool Restore(string name, string query, DateTimeOffset now)
    {
        if (RemovedAt is null) return false;
        RemovedAt = null;
        Name = EntityRules.Text(name, MaxNameLength, nameof(name));
        NormalizedName = Normalize(Name);
        Query = CleanQuery(query);
        UpdatedAt = now;
        return true;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public static string CleanScope(string? scope) => scope?.Trim().ToUpperInvariant() switch
    {
        "TASK" => "TASK",
        "REQUEST" => "REQUEST",
        _ => throw new ArgumentException("A saved filter belongs to the TASK or REQUEST list.", nameof(scope))
    };

    /// <summary>
    /// A query string is opaque to this layer, so only its shape is checked: printable, no newlines
    /// and no leading "?". It is never rendered as HTML and never concatenated into SQL — the list
    /// endpoint parses it as query parameters and validates each one itself.
    /// </summary>
    private static string CleanQuery(string? query)
    {
        var text = (query ?? "").Trim().TrimStart('?');
        if (text.Length > MaxQueryLength)
            throw new ArgumentException("The saved query is too long.", nameof(query));
        if (text.Any(char.IsControl))
            throw new ArgumentException("A saved query must be a plain query string.", nameof(query));
        return text;
    }
}
