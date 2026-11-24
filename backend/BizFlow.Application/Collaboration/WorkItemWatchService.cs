using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Collaboration;

public sealed record WorkItemParticipantInfo(Guid UserId, string FullName, string EmployeeCode, string? DepartmentName = null);

public sealed record WorkItemWatchStatus(
    string ObjectType,
    Guid ObjectId,
    bool Watching,
    int WatcherCount,
    IReadOnlyList<WorkItemParticipantInfo>? Watchers = null);

public sealed record WorkItemMentionResult(Guid CommentId, int Recorded, IReadOnlyList<Guid> Skipped);

public interface IWorkItemWatchStore
{
    /// <summary>True when the task is inside the given read scope. Reuses the task visibility gate.</summary>
    Task<bool> IsTaskVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken);

    /// <summary>True when the request is inside the given read scope.</summary>
    Task<bool> IsRequestVisibleAsync(RequestReadScope scope, Guid requestId, CancellationToken cancellationToken);

    Task<(bool Watching, int Count)> ReadStatusAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the active watchers with display identity for this work item.</summary>
    Task<IReadOnlyList<WorkItemParticipantInfo>> ReadWatchersAsync(Guid tenantId, CommentObjectType objectType,
        Guid objectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkItemParticipantInfo>>([]);

    /// <summary>Returns active users in this tenant for scoped mention/watcher candidate filtering.</summary>
    Task<IReadOnlyList<WorkItemParticipantInfo>> ReadActiveTenantDirectoryAsync(Guid tenantId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkItemParticipantInfo>>([]);

    /// <summary>Adds the watcher when absent. Returns false when it was already there.</summary>
    Task<bool> AddWatcherAsync(WorkItemWatcher watcher, CancellationToken cancellationToken);

    /// <summary>Stops the subscription. Returns false when there was nothing to stop.</summary>
    Task<bool> RemoveWatcherAsync(Guid tenantId, CommentObjectType objectType, Guid objectId, Guid userId,
        DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The comment, when it exists in this tenant and names this object.</summary>
    Task<Comment?> FindCommentAsync(Guid tenantId, CommentObjectType objectType, Guid objectId, Guid commentId,
        CancellationToken cancellationToken);

    /// <summary>The ids among the candidates that are active users of this tenant.</summary>
    Task<IReadOnlyList<Guid>> ActiveTenantUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> candidates,
        CancellationToken cancellationToken);

    /// <summary>Mentions already recorded for this comment, so a retry records each person once.</summary>
    Task<IReadOnlyList<Guid>> ExistingMentionsAsync(Guid tenantId, Guid commentId, CancellationToken cancellationToken);

    Task CommitMentionsAsync(IReadOnlyCollection<CommentMention> mentions,
        IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken);
}

/// <summary>
/// Watchers and "@" mentions (F3). Everything here is additive: the comment command, its endpoint
/// and the existing notification flow are untouched. Visibility is always re-derived from
/// <see cref="TaskReadPolicy"/> or <see cref="RequestReadPolicy"/>, both for the caller and for
/// every person about to be mentioned, so a mention can never disclose an item to someone who
/// could not already open it.
/// </summary>
public sealed class WorkItemWatchService(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IWorkItemWatchStore store,
    INotificationUpdates updates,
    TimeProvider clock)
{
    public const int MaxMentionsPerComment = 10;

    public static CommentObjectType ParseObjectType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "TASK" => CommentObjectType.Task,
        "REQUEST" => CommentObjectType.Request,
        _ => throw new ApplicationFault(FaultKind.Validation, "WATCH.OBJECT_TYPE",
            "Use TASK or REQUEST as the work item type.")
    };

    public async Task<WorkItemWatchStatus> GetStatusAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var (tenantId, userId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        var (watching, count) = await store.ReadStatusAsync(tenantId, type, objectId, userId, cancellationToken);
        var watchers = await store.ReadWatchersAsync(tenantId, type, objectId, cancellationToken);
        return new(Code(type), objectId, watching, count, watchers);
    }

    public async Task<WorkItemWatchStatus> WatchAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var (tenantId, userId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        // Idempotent on purpose: pressing "follow" twice is not an error.
        await store.AddWatcherAsync(WorkItemWatcher.Create(tenantId, type, objectId, userId, clock.GetUtcNow()), cancellationToken);
        var (watching, count) = await store.ReadStatusAsync(tenantId, type, objectId, userId, cancellationToken);
        var watchers = await store.ReadWatchersAsync(tenantId, type, objectId, cancellationToken);
        return new(Code(type), objectId, watching, count, watchers);
    }

    public async Task<WorkItemWatchStatus> UnwatchAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var (tenantId, userId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        await store.RemoveWatcherAsync(tenantId, type, objectId, userId, clock.GetUtcNow(), cancellationToken);
        var (watching, count) = await store.ReadStatusAsync(tenantId, type, objectId, userId, cancellationToken);
        var watchers = await store.ReadWatchersAsync(tenantId, type, objectId, cancellationToken);
        return new(Code(type), objectId, watching, count, watchers);
    }

    /// <summary>
    /// Returns active tenant employees who are authorized to view this specific work item,
    /// so @mention and watcher suggestions never expose unrelated or unauthorized users.
    /// </summary>
    public async Task<IReadOnlyList<WorkItemParticipantInfo>> GetMentionCandidatesAsync(string objectType, Guid objectId,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        var directory = await store.ReadActiveTenantDirectoryAsync(tenantId, cancellationToken);
        var eligible = new List<WorkItemParticipantInfo>();
        foreach (var candidate in directory)
        {
            if (candidate.UserId == Guid.Empty || candidate.UserId == userId) continue;
            if (await CanSeeAsync(candidate.UserId, tenantId, type, objectId, cancellationToken))
            {
                eligible.Add(candidate);
            }
        }
        return eligible;
    }

    /// <summary>
    /// Allows an authorized user to add another eligible participant (who can view the item) as a watcher.
    /// </summary>
    public async Task<WorkItemWatchStatus> AddParticipantWatcherAsync(string objectType, Guid objectId, Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        if (targetUserId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "WATCH.USER_REQUIRED", "A valid user id is required.");
        var members = await store.ActiveTenantUsersAsync(tenantId, [targetUserId], cancellationToken);
        if (!members.Contains(targetUserId) || !await CanSeeAsync(targetUserId, tenantId, type, objectId, cancellationToken))
            throw new ApplicationFault(FaultKind.Forbidden, "WATCH.USER_NOT_ELIGIBLE", "The selected user does not have access to view this work item.");
        await store.AddWatcherAsync(WorkItemWatcher.Create(tenantId, type, objectId, targetUserId, clock.GetUtcNow()), cancellationToken);
        var (watching, count) = await store.ReadStatusAsync(tenantId, type, objectId, userId, cancellationToken);
        var watchers = await store.ReadWatchersAsync(tenantId, type, objectId, cancellationToken);
        return new(Code(type), objectId, watching, count, watchers);
    }

    public async Task<WorkItemMentionResult> RecordMentionsAsync(string objectType, Guid objectId, Guid commentId,
        IReadOnlyCollection<Guid> mentionedUserIds, CancellationToken cancellationToken)
    {
        var (tenantId, userId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        if (commentId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "WATCH.COMMENT_REQUIRED", "A comment id is required.");
        var requested = mentionedUserIds.Where(id => id != Guid.Empty && id != userId).Distinct().ToArray();
        if (requested.Length == 0)
            throw new ApplicationFault(FaultKind.Validation, "WATCH.NO_MENTIONS",
                "Name at least one other person to mention.");
        if (requested.Length > MaxMentionsPerComment)
            throw new ApplicationFault(FaultKind.Validation, "WATCH.TOO_MANY_MENTIONS",
                $"A comment can mention at most {MaxMentionsPerComment} people.");

        var comment = await store.FindCommentAsync(tenantId, type, objectId, commentId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        if (comment.AuthorId != userId)
            throw new ApplicationFault(FaultKind.Forbidden, "WATCH.NOT_COMMENT_AUTHOR",
                "Only the comment author can record its mentions.");

        var members = await store.ActiveTenantUsersAsync(tenantId, requested, cancellationToken);
        var already = await store.ExistingMentionsAsync(tenantId, commentId, cancellationToken);
        var now = clock.GetUtcNow();
        var mentions = new List<CommentMention>();
        var notifications = new List<Notification>();
        var skipped = new List<Guid>();
        foreach (var candidate in requested)
        {
            if (!members.Contains(candidate) || already.Contains(candidate))
            {
                skipped.Add(candidate);
                continue;
            }
            // The decisive check: a mention must never reveal an item the recipient cannot open.
            if (!await CanSeeAsync(candidate, tenantId, type, objectId, cancellationToken))
            {
                skipped.Add(candidate);
                continue;
            }
            mentions.Add(CommentMention.Create(tenantId, commentId, type, objectId, candidate, userId, now));
            notifications.Add(Notification.Create(tenantId, candidate, NotificationEvent.Mentioned,
                "Bạn được nhắc đến trong một bình luận",
                Excerpt(comment.Content),
                $"mention:{commentId}:{candidate}", Code(type), objectId));
        }
        if (mentions.Count > 0)
        {
            await store.CommitMentionsAsync(mentions, notifications, cancellationToken);
            await updates.PublishAsync(tenantId, [.. mentions.Select(m => m.MentionedUserId)], cancellationToken);
        }
        return new(commentId, mentions.Count, skipped);
    }

    private static string Code(CommentObjectType type) => type == CommentObjectType.Task ? "TASK" : "REQUEST";

    private static string Excerpt(string content) =>
        content.Length <= 280 ? content : content[..280] + "…";

    private async Task<(Guid TenantId, Guid UserId, CommentObjectType Type)> RequireVisibleAsync(
        string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var type = ParseObjectType(objectType);
        if (objectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "WATCH.OBJECT_REQUIRED", "A work item id is required.");
        if (context.UserId is not { } userId || userId == Guid.Empty || context.TenantId is not { } tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
        var access = await snapshots.ResolveAsync(userId, tenantId, cancellationToken);
        if (access is null || access.UserId != userId || access.TenantId != tenantId)
        {
            await audit.RecordDeniedAccessAsync(userId, tenantId, Permission(type), AccessDenial.Unauthenticated, cancellationToken);
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
        }
        if (!await VisibleToAsync(access, type, objectId, cancellationToken))
        {
            await audit.RecordDeniedAccessAsync(userId, tenantId, Permission(type), AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        return (tenantId, userId, type);
    }

    private static string Permission(CommentObjectType type) => type == CommentObjectType.Task ? "tasks.read" : "requests.read";

    private async Task<bool> CanSeeAsync(Guid candidate, Guid tenantId, CommentObjectType type, Guid objectId,
        CancellationToken cancellationToken)
    {
        var access = await snapshots.ResolveAsync(candidate, tenantId, cancellationToken);
        return access is not null && access.UserId == candidate && access.TenantId == tenantId
            && await VisibleToAsync(access, type, objectId, cancellationToken);
    }

    private async Task<bool> VisibleToAsync(AccessSnapshot access, CommentObjectType type, Guid objectId,
        CancellationToken cancellationToken)
    {
        if (type == CommentObjectType.Task)
        {
            var (decision, scope) = TaskReadPolicy.Resolve(access);
            return decision.Allowed && scope is not null
                && await store.IsTaskVisibleAsync(scope, objectId, cancellationToken);
        }
        var (requestDecision, requestScope) = RequestReadPolicy.Resolve(access);
        return requestDecision.Allowed && requestScope is not null
            && await store.IsRequestVisibleAsync(requestScope, objectId, cancellationToken);
    }
}
