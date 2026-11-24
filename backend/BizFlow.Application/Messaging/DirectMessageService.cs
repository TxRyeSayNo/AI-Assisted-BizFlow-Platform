using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Messaging;

namespace BizFlow.Application.Messaging;

public sealed record ConversationRow(
    Guid ConversationId,
    Guid OtherUserId,
    string OtherUserName,
    string OtherUserEmployeeCode,
    string? LastMessagePreview,
    Guid? LastMessageSenderId,
    DateTimeOffset? LastMessageAt,
    long UnreadCount);

public sealed record ConversationPage(IReadOnlyList<ConversationRow> Items, int Page, int PageSize, long Total);

public sealed record MessageRow(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Body,
    bool Deleted,
    bool Mine,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt);

public sealed record MessagePage(Guid ConversationId, IReadOnlyList<MessageRow> Items, int Page, int PageSize, long Total);

/// <summary>Carries a hint only. The message body is never pushed over the socket.</summary>
public interface IDirectMessageUpdates
{
    Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientIds, Guid conversationId,
        CancellationToken cancellationToken);
}

public interface IDirectMessageStore
{
    Task<(Guid UserId, string FullName, string EmployeeCode)?> FindActiveColleagueAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken);
    Task<ChatConversation?> FindDirectConversationAsync(Guid tenantId, string pairKey, CancellationToken cancellationToken);
    Task<ChatConversation?> FindConversationForParticipantAsync(Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken cancellationToken);
    Task<ChatParticipant?> FindParticipantAsync(Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ParticipantsAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken);
    Task AddConversationAsync(ChatConversation conversation, IReadOnlyCollection<ChatParticipant> participants,
        CancellationToken cancellationToken);
    Task<ConversationPage> ListConversationsAsync(Guid tenantId, Guid userId, PageRequest page,
        CancellationToken cancellationToken);
    Task<MessagePage> ListMessagesAsync(Guid tenantId, Guid conversationId, Guid userId, PageRequest page,
        CancellationToken cancellationToken);
    Task<MessageRow?> FindReplayAsync(Guid tenantId, Guid conversationId, Guid senderId, string fingerprint,
        CancellationToken cancellationToken);
    Task<MessageRow> AddMessageAsync(ChatMessage message, string? fingerprint, CancellationToken cancellationToken);
    Task<ChatMessage?> FindOwnMessageAsync(Guid tenantId, Guid conversationId, Guid messageId, Guid senderId,
        CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Direct messages between colleagues (F13).
///
/// Everything is new: no existing table, endpoint, command or notification flow is touched.
/// Messaging is a capability of every active member of a tenant rather than an administrative
/// one, so it is gated by <see cref="TenantMembershipAuthorizer"/>; on top of that, every read and
/// write re-checks that the caller is a participant of that specific conversation, so one person
/// can never reach another pair's messages even inside the same tenant.
///
/// Privacy: an administrator gets no special read path here, and no message body is ever written
/// to the audit trail. The realtime channel carries a conversation id only, never the words.
/// </summary>
public sealed class DirectMessageService(
    ITenantContext context,
    TenantMembershipAuthorizer membership,
    IDirectMessageStore store,
    IDirectMessageUpdates updates,
    TimeProvider clock)
{
    private const string Operation = "notifications.read";

    public async Task<ConversationPage> ListConversationsAsync(PageRequest page, CancellationToken cancellationToken)
    {
        // PageRequest validates itself on construction, so an out-of-range page never reaches here.
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        return await store.ListConversationsAsync(tenantId, userId, page, cancellationToken);
    }

    /// <summary>
    /// Finds the conversation with a colleague, creating it the first time. Idempotent by
    /// construction: the canonical pair key has a unique index, so two people always share one.
    /// </summary>
    public async Task<ConversationRow> OpenDirectAsync(Guid otherUserId, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        if (otherUserId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "CHAT.RECIPIENT_REQUIRED", "Choose a colleague to message.");
        if (otherUserId == userId)
            throw new ApplicationFault(FaultKind.Validation, "CHAT.SELF", "You cannot open a conversation with yourself.");

        // The colleague has to be an active member of this very tenant, which is also what stops
        // a probe for user ids from another workspace.
        var colleague = await store.FindActiveColleagueAsync(tenantId, otherUserId, cancellationToken)
            ?? throw ApplicationFault.NotFound();

        var pairKey = ChatConversation.Pair(userId, otherUserId);
        var existing = await store.FindDirectConversationAsync(tenantId, pairKey, cancellationToken);
        if (existing is null)
        {
            var now = clock.GetUtcNow();
            var conversation = ChatConversation.CreateDirect(tenantId, userId, otherUserId, userId, now);
            await store.AddConversationAsync(conversation,
            [
                ChatParticipant.Create(tenantId, conversation.Id, userId, now),
                ChatParticipant.Create(tenantId, conversation.Id, otherUserId, now)
            ], cancellationToken);
            existing = conversation;
        }
        return new(existing.Id, colleague.UserId, colleague.FullName, colleague.EmployeeCode,
            null, null, existing.LastMessageAt, 0);
    }

    public async Task<MessagePage> ListMessagesAsync(Guid conversationId, PageRequest page,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        await RequireParticipantAsync(tenantId, conversationId, userId, cancellationToken);
        return await store.ListMessagesAsync(tenantId, conversationId, userId, page, cancellationToken);
    }

    public async Task<MessageRow> SendAsync(Guid conversationId, string body, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        var conversation = await RequireParticipantAsync(tenantId, conversationId, userId, cancellationToken);
        var clean = (body ?? "").Trim();
        if (clean.Length == 0 || clean.Length > ChatMessage.MaxBodyLength)
            throw new ApplicationFault(FaultKind.Validation, "CHAT.BODY",
                $"A message must contain 1 to {ChatMessage.MaxBodyLength} characters.");
        if (clean.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ApplicationFault(FaultKind.Validation, "CHAT.BODY", "Use plain text for a message.");

        // Replaying the same key returns the message that was already stored, so a retried send
        // after an uncertain response cannot duplicate it.
        var fingerprint = Fingerprint(idempotencyKey, conversationId, clean);
        if (fingerprint is not null)
        {
            var replay = await store.FindReplayAsync(tenantId, conversationId, userId, fingerprint, cancellationToken);
            if (replay is not null) return replay;
        }

        var now = clock.GetUtcNow();
        var message = ChatMessage.Create(tenantId, conversationId, userId, clean, now);
        conversation.Touch(now);
        var stored = await store.AddMessageAsync(message, fingerprint, cancellationToken);

        // Post-commit, best effort, and only ever a hint: recipients refetch over authenticated
        // HTTP, where participation is checked again.
        var others = (await store.ParticipantsAsync(tenantId, conversationId, cancellationToken))
            .Where(id => id != userId).ToArray();
        if (others.Length > 0)
            await updates.PublishAsync(tenantId, others, conversationId, cancellationToken);
        return stored;
    }

    public async Task<MessageRow> EditAsync(Guid conversationId, Guid messageId, string body,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        await RequireParticipantAsync(tenantId, conversationId, userId, cancellationToken);
        var message = await store.FindOwnMessageAsync(tenantId, conversationId, messageId, userId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        if (message.DeletedAt is not null)
            throw new ApplicationFault(FaultKind.Validation, "CHAT.DELETED", "A deleted message cannot be edited.");
        var clean = (body ?? "").Trim();
        if (clean.Length == 0 || clean.Length > ChatMessage.MaxBodyLength)
            throw new ApplicationFault(FaultKind.Validation, "CHAT.BODY",
                $"A message must contain 1 to {ChatMessage.MaxBodyLength} characters.");
        if (message.Edit(clean, clock.GetUtcNow())) await store.SaveAsync(cancellationToken);
        var others = (await store.ParticipantsAsync(tenantId, conversationId, cancellationToken))
            .Where(id => id != userId).ToArray();
        if (others.Length > 0)
            await updates.PublishAsync(tenantId, others, conversationId, cancellationToken);
        return Map(message, userId, "");
    }

    /// <summary>Removes the words but keeps the row, so the conversation keeps its shape.</summary>
    public async Task<MessageRow> DeleteAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        await RequireParticipantAsync(tenantId, conversationId, userId, cancellationToken);
        var message = await store.FindOwnMessageAsync(tenantId, conversationId, messageId, userId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        if (message.Delete(clock.GetUtcNow())) await store.SaveAsync(cancellationToken);
        var others = (await store.ParticipantsAsync(tenantId, conversationId, cancellationToken))
            .Where(id => id != userId).ToArray();
        if (others.Length > 0)
            await updates.PublishAsync(tenantId, others, conversationId, cancellationToken);
        return Map(message, userId, "");
    }

    public async Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireMemberAsync(cancellationToken);
        await RequireParticipantAsync(tenantId, conversationId, userId, cancellationToken);
        var participant = await store.FindParticipantAsync(tenantId, conversationId, userId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        if (participant.MarkRead(clock.GetUtcNow())) await store.SaveAsync(cancellationToken);
    }

    internal static MessageRow Map(ChatMessage message, Guid viewerId, string senderName) =>
        new(message.Id, message.ConversationId, message.SenderId, senderName,
            message.DeletedAt is null ? message.Body : "", message.DeletedAt is not null,
            message.SenderId == viewerId, message.CreatedAt, message.EditedAt);

    private static string? Fingerprint(string? idempotencyKey, Guid conversationId, string body) =>
        string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{idempotencyKey.Trim()}|{conversationId:N}|{body}")));

    private async Task<(Guid TenantId, Guid UserId)> RequireMemberAsync(CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync(Operation, cancellationToken);
        return (tenantId, context.UserId!.Value);
    }

    /// <summary>
    /// Membership of the tenant is not enough: the caller has to be in this conversation. A
    /// conversation they are not part of is reported as missing, never as forbidden, so the
    /// endpoint cannot be used to discover that two colleagues are talking.
    /// </summary>
    private async Task<ChatConversation> RequireParticipantAsync(Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "CHAT.CONVERSATION_REQUIRED",
                "A conversation id is required.");
        return await store.FindConversationForParticipantAsync(tenantId, conversationId, userId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
    }
}
