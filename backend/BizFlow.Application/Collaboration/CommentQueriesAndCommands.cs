using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Collaboration;

namespace BizFlow.Application.Collaboration;

public sealed record CommentItemView(
    Guid CommentId,
    string ObjectType,
    Guid ObjectId,
    Guid AuthorId,
    string AuthorName,
    string AuthorEmail,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    bool IsOwner,
    bool CanDelete);

public sealed record CreateCommentCommand(
    string ObjectType,
    Guid ObjectId,
    string Content);

public sealed record EditCommentCommand(
    string Content);

public sealed record StoredCommentCreation(
    string Fingerprint,
    CommentItemView Result);

public interface ICommentCreationTransaction : IAsyncDisposable
{
    Task<StoredCommentCreation?> FindReplayAsync(CancellationToken cancellationToken);
    Task<bool> ValidateTargetExistsAsync(CommentObjectType objectType, Guid objectId, CancellationToken cancellationToken);
    Task CommitAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken);
}

public interface ICommentStore
{
    Task<ICommentCreationTransaction> BeginCreateAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken);
    Task<Comment?> FindCommentAsync(Guid tenantId, Guid commentId, CancellationToken cancellationToken);
    Task UpdateCommentAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken);
    Task DeleteCommentAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken);
    Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public interface ICommentReader
{
    Task<IReadOnlyList<CommentItemView>> ListCommentsAsync(
        Guid tenantId,
        CommentObjectType objectType,
        Guid objectId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<CommentItemView?> GetCommentByIdAsync(
        Guid tenantId,
        Guid commentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);
}

public static class CommentRules
{
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string? KeyHash(string? key)
    {
        if (key is null) return null;
        if (key.Length is < 1 or > 128 || key.Any(c => c is < '!' or > '~'))
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Use one opaque key of 1 to 128 visible ASCII characters.");
        return Hash(key);
    }

    public static CommentObjectType ParseObjectType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "ObjectType is required.");

        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "TASK" => CommentObjectType.Task,
            "REQUEST" => CommentObjectType.Request,
            "RESULT" => CommentObjectType.Result,
            "PROGRESS" => CommentObjectType.Progress,
            _ => throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", $"Invalid ObjectType '{value}'. Allowed: TASK, REQUEST, RESULT, PROGRESS.")
        };
    }
}

public sealed class CommentService(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    ICommentStore store,
    ICommentReader reader,
    TimeProvider clock)
{
    private Task AuthorizeCreateAsync(CancellationToken cancellationToken) =>
        authorizer.AuthorizeAsync("comments.create", new(context.TenantId), cancellationToken: cancellationToken);

    private Task AuthorizeReadAsync(CancellationToken cancellationToken) =>
        authorizer.AuthorizeAsync("comments.read", new(context.TenantId), cancellationToken: cancellationToken);

    public async Task<CommentItemView> CreateAsync(CreateCommentCommand command, string? key, CancellationToken cancellationToken)
    {
        await AuthorizeCreateAsync(cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Commenting requires an active tenant workspace.");

        if (command.ObjectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "ObjectId must not be empty.");

        if (string.IsNullOrWhiteSpace(command.Content))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Comment content cannot be empty.");

        var trimmedContent = command.Content.Trim();
        if (trimmedContent.Length > 4000)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Comment content cannot exceed 4000 characters.");

        var objectType = CommentRules.ParseObjectType(command.ObjectType);
        var keyHash = CommentRules.KeyHash(key);
        var fingerprint = CommentRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            objectType = objectType.ToString().ToUpperInvariant(),
            objectId = command.ObjectId,
            content = trimmedContent
        }));

        await using var transaction = await store.BeginCreateAsync(tenantId, actorId, keyHash, cancellationToken);
        await AuthorizeCreateAsync(cancellationToken);

        if (keyHash is not null && await transaction.FindReplayAsync(cancellationToken) is { } replay)
        {
            if (replay.Fingerprint != fingerprint)
                throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different comment input.");
            return replay.Result;
        }

        var targetExists = await transaction.ValidateTargetExistsAsync(objectType, command.ObjectId, cancellationToken);
        if (!targetExists)
            throw new ApplicationFault(FaultKind.NotFound, "TARGET.NOT_FOUND", $"Target {objectType} not found in current tenant.");

        var now = clock.GetUtcNow();
        var comment = Comment.Create(tenantId, objectType, command.ObjectId, actorId, trimmedContent, now);
        var audit = AuditLog.CommentCreated(comment, now, keyHash, keyHash is null ? null : fingerprint);

        await transaction.CommitAsync(comment, audit, cancellationToken);

        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);
        var createdView = await reader.GetCommentByIdAsync(tenantId, comment.Id, actorId, isAdmin, cancellationToken);
        return createdView ?? new CommentItemView(
            comment.Id,
            comment.ObjectType.ToString().ToUpperInvariant(),
            comment.ObjectId,
            comment.AuthorId,
            "User",
            string.Empty,
            comment.Content,
            comment.CreatedAt,
            comment.EditedAt,
            IsOwner: true,
            CanDelete: true);
    }

    public async Task<CommentItemView> EditAsync(Guid commentId, EditCommentCommand command, CancellationToken cancellationToken)
    {
        await AuthorizeCreateAsync(cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Commenting requires an active tenant workspace.");

        if (string.IsNullOrWhiteSpace(command.Content))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Comment content cannot be empty.");

        var trimmed = command.Content.Trim();
        if (trimmed.Length > 4000)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Comment content cannot exceed 4000 characters.");

        var comment = await store.FindCommentAsync(tenantId, commentId, cancellationToken);
        if (comment is null || comment.DeletedAt != null)
            throw ApplicationFault.NotFound();

        if (comment.AuthorId != actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "COMMENT.NOT_OWNER", "Only the author can edit this comment.");

        var previousContent = comment.Content;
        var now = clock.GetUtcNow();
        comment.Edit(trimmed, now);
        var audit = AuditLog.CommentEdited(comment, previousContent, now);

        await store.UpdateCommentAsync(comment, audit, cancellationToken);

        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);
        var updatedView = await reader.GetCommentByIdAsync(tenantId, comment.Id, actorId, isAdmin, cancellationToken);
        return updatedView!;
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken cancellationToken)
    {
        await AuthorizeCreateAsync(cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Commenting requires an active tenant workspace.");

        var comment = await store.FindCommentAsync(tenantId, commentId, cancellationToken);
        if (comment is null || comment.DeletedAt != null)
            throw ApplicationFault.NotFound();

        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);
        if (comment.AuthorId != actorId && !isAdmin)
            throw new ApplicationFault(FaultKind.Forbidden, "COMMENT.DELETE_DENIED", "Only the author or a company administrator can delete this comment.");

        var now = clock.GetUtcNow();
        comment.Delete(now);
        var audit = AuditLog.CommentDeleted(comment, actorId, now);

        await store.DeleteCommentAsync(comment, audit, cancellationToken);
    }

    public async Task<IReadOnlyList<CommentItemView>> ListAsync(string objectTypeStr, Guid objectId, CancellationToken cancellationToken)
    {
        await AuthorizeReadAsync(cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Viewing comments requires an active tenant workspace.");

        if (objectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "ObjectId must not be empty.");

        var objectType = CommentRules.ParseObjectType(objectTypeStr);
        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);

        return await reader.ListCommentsAsync(tenantId, objectType, objectId, actorId, isAdmin, cancellationToken);
    }
}
