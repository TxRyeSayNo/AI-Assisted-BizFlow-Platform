using BizFlow.Domain.Common;

namespace BizFlow.Domain.Messaging;

/// <summary>
/// A conversation between colleagues of one tenant (F13).
///
/// The shape is conversation + participants rather than a sender/recipient pair, so group chat is
/// a later addition rather than a rewrite. For a two-person chat <see cref="PairKey"/> holds the
/// two user ids in a canonical order, and a unique index on it guarantees the same two people
/// always land in one conversation however many times either of them opens it.
/// </summary>
public sealed class ChatConversation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    /// <summary>Canonical "smaller:larger" user id pair for a direct chat; null for a group.</summary>
    public string? PairKey { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>Drives the conversation list order without counting messages on every read.</summary>
    public DateTimeOffset? LastMessageAt { get; private set; }

    private ChatConversation() { }

    public static ChatConversation CreateDirect(Guid tenantId, Guid firstUserId, Guid secondUserId,
        Guid createdBy, DateTimeOffset now)
    {
        EntityRules.Id(firstUserId, nameof(firstUserId));
        EntityRules.Id(secondUserId, nameof(secondUserId));
        if (firstUserId == secondUserId)
            throw new ArgumentException("A direct conversation needs two different people.", nameof(secondUserId));
        if (createdBy != firstUserId && createdBy != secondUserId)
            throw new ArgumentException("Only a participant can open a direct conversation.", nameof(createdBy));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            PairKey = Pair(firstUserId, secondUserId),
            CreatedBy = createdBy,
            CreatedAt = now
        };
    }

    /// <summary>Order-independent key, so (a,b) and (b,a) are the same conversation.</summary>
    public static string Pair(Guid firstUserId, Guid secondUserId)
    {
        var first = firstUserId.ToString("N");
        var second = secondUserId.ToString("N");
        return string.CompareOrdinal(first, second) <= 0 ? $"{first}:{second}" : $"{second}:{first}";
    }

    public bool Touch(DateTimeOffset at)
    {
        if (LastMessageAt is { } last && last >= at) return false;
        LastMessageAt = at;
        return true;
    }
}

/// <summary>
/// Someone's membership of a conversation (F13). Leaving is a soft change, like everything else
/// here: the platform keeps history rather than hard-deleting it.
/// </summary>
public sealed class ChatParticipant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    /// <summary>Everything created at or before this instant counts as read for this person.</summary>
    public DateTimeOffset? LastReadAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }

    private ChatParticipant() { }

    public static ChatParticipant Create(Guid tenantId, Guid conversationId, Guid userId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        ConversationId = EntityRules.Id(conversationId, nameof(conversationId)),
        UserId = EntityRules.Id(userId, nameof(userId)),
        JoinedAt = now
    };

    public bool MarkRead(DateTimeOffset at)
    {
        if (LastReadAt is { } read && read >= at) return false;
        LastReadAt = at;
        return true;
    }
}

/// <summary>
/// One message (F13). Editing and deleting are soft so a conversation keeps its shape; a deleted
/// message keeps its row and stops carrying a body.
/// </summary>
public sealed class ChatMessage
{
    public const int MaxBodyLength = 4000;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private ChatMessage() { }

    public static ChatMessage Create(Guid tenantId, Guid conversationId, Guid senderId, string body,
        DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            ConversationId = EntityRules.Id(conversationId, nameof(conversationId)),
            SenderId = EntityRules.Id(senderId, nameof(senderId)),
            Body = Text(body),
            CreatedAt = now
        };

    public bool Edit(string body, DateTimeOffset now)
    {
        if (DeletedAt is not null)
            throw new InvalidOperationException("A deleted message cannot be edited.");
        var clean = Text(body);
        if (clean == Body) return false;
        Body = clean;
        EditedAt = now;
        return true;
    }

    public bool Delete(DateTimeOffset now)
    {
        if (DeletedAt is not null) return false;
        // The row stays for the conversation's shape, but the words do not.
        Body = "";
        DeletedAt = now;
        return true;
    }

    private static string Text(string body)
    {
        var clean = (body ?? "").Trim();
        if (clean.Length == 0 || clean.Length > MaxBodyLength)
            throw new ArgumentException($"A message must contain 1 to {MaxBodyLength} characters.", nameof(body));
        if (clean.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Use plain text for a message.", nameof(body));
        return clean;
    }
}
