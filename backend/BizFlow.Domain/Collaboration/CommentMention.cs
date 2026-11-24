using BizFlow.Domain.Common;

namespace BizFlow.Domain.Collaboration;

/// <summary>
/// Records that a comment named someone with "@" (F3). The mention is stored separately from the
/// comment so the existing comment entity, its command and its API stay exactly as they are.
/// A mention is a notification trigger, never an access grant.
/// </summary>
public sealed class CommentMention
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CommentId { get; private set; }
    public CommentObjectType ObjectType { get; private set; }
    public Guid ObjectId { get; private set; }
    public Guid MentionedUserId { get; private set; }
    public Guid MentionedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CommentMention() { }

    public static CommentMention Create(Guid tenantId, Guid commentId, CommentObjectType objectType,
        Guid objectId, Guid mentionedUserId, Guid mentionedByUserId, DateTimeOffset now)
    {
        if (objectType is not (CommentObjectType.Task or CommentObjectType.Request))
            throw new ArgumentException("Only a task or a request comment can carry a mention.", nameof(objectType));
        if (mentionedUserId == mentionedByUserId)
            throw new ArgumentException("A comment cannot mention its own author.", nameof(mentionedUserId));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            CommentId = EntityRules.Id(commentId, nameof(commentId)),
            ObjectType = objectType,
            ObjectId = EntityRules.Id(objectId, nameof(objectId)),
            MentionedUserId = EntityRules.Id(mentionedUserId, nameof(mentionedUserId)),
            MentionedByUserId = EntityRules.Id(mentionedByUserId, nameof(mentionedByUserId)),
            CreatedAt = now
        };
    }
}
