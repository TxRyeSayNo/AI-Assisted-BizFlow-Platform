using BizFlow.Domain.Common;
using System.Text.RegularExpressions;

namespace BizFlow.Domain.Collaboration;

/// <summary>
/// A tenant-owned tag with a colour (F6). Labels are never hard-deleted — the platform keeps
/// history — so retiring one archives it and leaves the items that carried it intact.
/// </summary>
public sealed partial class Label
{
    public const int MaxNameLength = 60;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>Upper-case name used for the per-tenant uniqueness check.</summary>
    public string NormalizedName { get; private set; } = "";
    /// <summary>A #RRGGBB colour, stored upper-case.</summary>
    public string Colour { get; private set; } = "";
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    private Label() { }

    public static Label Create(Guid tenantId, string name, string colour, Guid createdBy, DateTimeOffset now)
    {
        var cleanName = EntityRules.Text(name, MaxNameLength, nameof(name));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            Name = cleanName,
            NormalizedName = Normalize(cleanName),
            Colour = Colour6(colour),
            CreatedBy = EntityRules.Id(createdBy, nameof(createdBy)),
            CreatedAt = now
        };
    }

    public bool Rename(string name, string colour, DateTimeOffset now)
    {
        var cleanName = EntityRules.Text(name, MaxNameLength, nameof(name));
        var cleanColour = Colour6(colour);
        if (cleanName == Name && cleanColour == Colour) return false;
        Name = cleanName;
        NormalizedName = Normalize(cleanName);
        Colour = cleanColour;
        UpdatedAt = now;
        return true;
    }

    public bool Archive(DateTimeOffset now)
    {
        if (ArchivedAt is not null) return false;
        ArchivedAt = now;
        UpdatedAt = now;
        return true;
    }

    public bool Restore(DateTimeOffset now)
    {
        if (ArchivedAt is null) return false;
        ArchivedAt = null;
        UpdatedAt = now;
        return true;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    /// <summary>Normalises a colour to #RRGGBB, or returns null when it is not one.</summary>
    public static string? TryColour(string? value)
    {
        var text = (value ?? "").Trim().ToUpperInvariant();
        if (text.Length > 0 && !text.StartsWith('#')) text = "#" + text;
        return HexColour().IsMatch(text) ? text : null;
    }

    private static string Colour6(string value) => TryColour(value)
        ?? throw new ArgumentException("A label colour must be a #RRGGBB value.", nameof(value));

    [GeneratedRegex("^#[0-9A-F]{6}$")]
    private static partial Regex HexColour();
}

/// <summary>
/// Attaches a <see cref="Label"/> to one task or request (F6). Detaching is a soft change, for the
/// same reason labels are archived rather than deleted.
/// </summary>
public sealed class WorkItemLabel
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public CommentObjectType ObjectType { get; private set; }
    public Guid ObjectId { get; private set; }
    public Guid LabelId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }

    private WorkItemLabel() { }

    public static WorkItemLabel Create(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid labelId, Guid createdBy, DateTimeOffset now)
    {
        if (objectType is not (CommentObjectType.Task or CommentObjectType.Request))
            throw new ArgumentException("Only a task or a request can carry labels.", nameof(objectType));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            ObjectType = objectType,
            ObjectId = EntityRules.Id(objectId, nameof(objectId)),
            LabelId = EntityRules.Id(labelId, nameof(labelId)),
            CreatedBy = EntityRules.Id(createdBy, nameof(createdBy)),
            CreatedAt = now
        };
    }

    public bool Remove(DateTimeOffset now)
    {
        if (RemovedAt is not null) return false;
        RemovedAt = now;
        return true;
    }

    /// <summary>
    /// Re-applies a label that had been taken off. Only <see cref="RemovedAt"/> moves: the
    /// platform treats identity and creation stamps as immutable.
    /// </summary>
    public bool Restore()
    {
        if (RemovedAt is null) return false;
        RemovedAt = null;
        return true;
    }
}
