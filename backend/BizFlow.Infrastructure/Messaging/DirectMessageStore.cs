using BizFlow.Application.Common;
using BizFlow.Application.Messaging;
using BizFlow.Domain.Messaging;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Messaging;

/// <summary>
/// Storage for direct messages (F13). Every query is bounded by the caller's own participation,
/// on top of the fail-closed tenant filters already configured on the context, so a conversation
/// the caller is not part of is simply not in any result set.
/// </summary>
public sealed class DirectMessageStore(BizFlowDbContext db) : IDirectMessageStore
{
    public async Task<(Guid UserId, string FullName, string EmployeeCode)?> FindActiveColleagueAsync(Guid tenantId,
        Guid userId, CancellationToken cancellationToken)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Id == userId && u.Status == UserStatus.Active)
            .Select(u => new { u.Id, u.FullName, u.EmployeeCode })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.Id, row.FullName, row.EmployeeCode);
    }

    public Task<ChatConversation?> FindDirectConversationAsync(Guid tenantId, string pairKey,
        CancellationToken cancellationToken) =>
        db.ChatConversations.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.PairKey == pairKey,
            cancellationToken);

    public Task<ChatConversation?> FindConversationForParticipantAsync(Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken cancellationToken) =>
        db.ChatConversations.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId &&
            db.ChatParticipants.Any(p => p.ConversationId == c.Id && p.UserId == userId && p.LeftAt == null),
            cancellationToken);

    public Task<ChatParticipant?> FindParticipantAsync(Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken cancellationToken) =>
        db.ChatParticipants.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.ConversationId == conversationId &&
            p.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ParticipantsAsync(Guid tenantId, Guid conversationId,
        CancellationToken cancellationToken) =>
        await db.ChatParticipants.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.ConversationId == conversationId && p.LeftAt == null)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

    public async Task AddConversationAsync(ChatConversation conversation,
        IReadOnlyCollection<ChatParticipant> participants, CancellationToken cancellationToken)
    {
        db.ChatConversations.Add(conversation);
        db.ChatParticipants.AddRange(participants);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationPage> ListConversationsAsync(Guid tenantId, Guid userId, PageRequest page,
        CancellationToken cancellationToken)
    {
        var mine = db.ChatConversations.AsNoTracking()
            .Where(c => c.TenantId == tenantId &&
                db.ChatParticipants.Any(p => p.ConversationId == c.Id && p.UserId == userId && p.LeftAt == null));

        var total = await mine.LongCountAsync(cancellationToken);
        // Newest activity first; a conversation with no message yet falls back to when it opened.
        var rows = await mine
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ThenBy(c => c.Id)
            .Skip(page.Offset).Take(page.PageSize)
            .Select(c => new
            {
                c.Id,
                c.LastMessageAt,
                Mine = db.ChatParticipants.Where(p => p.ConversationId == c.Id && p.UserId == userId)
                    .Select(p => p.LastReadAt).FirstOrDefault(),
                Other = db.ChatParticipants.Where(p => p.ConversationId == c.Id && p.UserId != userId && p.LeftAt == null)
                    .Select(p => new
                    {
                        p.UserId,
                        Name = db.Users.Where(u => u.TenantId == tenantId && u.Id == p.UserId).Select(u => u.FullName).FirstOrDefault(),
                        Code = db.Users.Where(u => u.TenantId == tenantId && u.Id == p.UserId).Select(u => u.EmployeeCode).FirstOrDefault()
                    }).FirstOrDefault(),
                Last = db.ChatMessages.Where(m => m.ConversationId == c.Id)
                    .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                    .Select(m => new { m.SenderId, m.Body, m.DeletedAt }).FirstOrDefault(),
                Unread = db.ChatMessages.LongCount(m => m.ConversationId == c.Id && m.SenderId != userId &&
                    m.DeletedAt == null &&
                    (db.ChatParticipants.Where(p => p.ConversationId == c.Id && p.UserId == userId)
                        .Select(p => p.LastReadAt).FirstOrDefault() == null ||
                     m.CreatedAt > db.ChatParticipants.Where(p => p.ConversationId == c.Id && p.UserId == userId)
                        .Select(p => p.LastReadAt).FirstOrDefault()))
            })
            .ToListAsync(cancellationToken);

        return new([.. rows.Select(r => new ConversationRow(
            r.Id,
            r.Other?.UserId ?? Guid.Empty,
            r.Other?.Name ?? "",
            r.Other?.Code ?? "",
            r.Last is null ? null : r.Last.DeletedAt is null ? Preview(r.Last.Body) : null,
            r.Last?.SenderId,
            r.LastMessageAt,
            r.Unread))], page.Page, page.PageSize, total);
    }

    public async Task<MessagePage> ListMessagesAsync(Guid tenantId, Guid conversationId, Guid userId, PageRequest page,
        CancellationToken cancellationToken)
    {
        var all = db.ChatMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId);
        var total = await all.LongCountAsync(cancellationToken);
        // Newest page first, so opening a long conversation does not read its whole history.
        var rows = await all
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip(page.Offset).Take(page.PageSize)
            .Select(m => new
            {
                m.Id, m.ConversationId, m.SenderId, m.Body, m.CreatedAt, m.EditedAt, m.DeletedAt,
                SenderName = db.Users.Where(u => u.TenantId == tenantId && u.Id == m.SenderId)
                    .Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new(conversationId, [.. rows.Select(m => new MessageRow(
            m.Id, m.ConversationId, m.SenderId, m.SenderName ?? "",
            m.DeletedAt is null ? m.Body : "", m.DeletedAt is not null, m.SenderId == userId,
            m.CreatedAt, m.EditedAt))], page.Page, page.PageSize, total);
    }

    public async Task<MessageRow?> FindReplayAsync(Guid tenantId, Guid conversationId, Guid senderId,
        string fingerprint, CancellationToken cancellationToken)
    {
        var row = await db.ChatMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId && m.SenderId == senderId &&
                EF.Property<string?>(m, "Fingerprint") == fingerprint)
            .Select(m => new
            {
                m.Id, m.ConversationId, m.SenderId, m.Body, m.CreatedAt, m.EditedAt, m.DeletedAt,
                SenderName = db.Users.Where(u => u.TenantId == tenantId && u.Id == m.SenderId)
                    .Select(u => u.FullName).FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.Id, row.ConversationId, row.SenderId, row.SenderName ?? "",
            row.DeletedAt is null ? row.Body : "", row.DeletedAt is not null, true, row.CreatedAt, row.EditedAt);
    }

    public async Task<MessageRow> AddMessageAsync(ChatMessage message, string? fingerprint,
        CancellationToken cancellationToken)
    {
        var entry = db.ChatMessages.Add(message);
        // The replay key is storage-only: it never appears on the domain entity or the API.
        entry.Property<string?>("Fingerprint").CurrentValue = fingerprint;
        await db.SaveChangesAsync(cancellationToken);
        var senderName = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == message.TenantId && u.Id == message.SenderId)
            .Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        return DirectMessageMapping.Map(message, message.SenderId, senderName ?? "");
    }

    public Task<ChatMessage?> FindOwnMessageAsync(Guid tenantId, Guid conversationId, Guid messageId, Guid senderId,
        CancellationToken cancellationToken) =>
        db.ChatMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.ConversationId == conversationId &&
            m.Id == messageId && m.SenderId == senderId, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static string Preview(string body) =>
        body.Length <= 120 ? body : body[..120] + "…";
}

/// <summary>Shared mapping so the store and the service describe a message identically.</summary>
internal static class DirectMessageMapping
{
    internal static MessageRow Map(ChatMessage message, Guid viewerId, string senderName) =>
        new(message.Id, message.ConversationId, message.SenderId, senderName,
            message.DeletedAt is null ? message.Body : "", message.DeletedAt is not null,
            message.SenderId == viewerId, message.CreatedAt, message.EditedAt);
}
