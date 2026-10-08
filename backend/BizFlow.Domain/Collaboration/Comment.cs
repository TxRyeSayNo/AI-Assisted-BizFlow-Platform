using BizFlow.Domain.Common;

namespace BizFlow.Domain.Collaboration;

public sealed class Comment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public CommentObjectType ObjectType { get; private set; }
    public Guid ObjectId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Comment() { }

    public static Comment Create(
        Guid tenantId,
        CommentObjectType objectType,
        Guid objectId,
        Guid authorId,
        string content,
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (objectId == Guid.Empty)
            throw new ArgumentException("Object ID is required.", nameof(objectId));
        if (authorId == Guid.Empty)
            throw new ArgumentException("Author ID is required.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty.", nameof(content));

        var trimmed = content.Trim();
        if (trimmed.Length > 4000)
            throw new ArgumentException("Comment content cannot exceed 4000 characters.", nameof(content));

        return new Comment
        {
            Id = id ?? Guid.CreateVersion7(),
            TenantId = tenantId,
            ObjectType = objectType,
            ObjectId = objectId,
            AuthorId = authorId,
            Content = trimmed,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void Edit(string newContent, DateTimeOffset? editedAt = null)
    {
        if (DeletedAt != null)
            throw new InvalidOperationException("Cannot edit a deleted comment.");
        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Comment content cannot be empty.", nameof(newContent));

        var trimmed = newContent.Trim();
        if (trimmed.Length > 4000)
            throw new ArgumentException("Comment content cannot exceed 4000 characters.", nameof(newContent));

        Content = trimmed;
        EditedAt = editedAt ?? DateTimeOffset.UtcNow;
    }

    public void Delete(DateTimeOffset? deletedAt = null)
    {
        if (DeletedAt != null)
            return;
        DeletedAt = deletedAt ?? DateTimeOffset.UtcNow;
    }
}
