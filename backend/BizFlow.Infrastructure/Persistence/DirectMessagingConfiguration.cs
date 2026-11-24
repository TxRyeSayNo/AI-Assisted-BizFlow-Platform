using BizFlow.Domain.Messaging;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

/// <summary>F13 storage: conversations, their participants and the messages.</summary>
internal sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> b)
    {
        b.ToTable("ChatConversation");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("ChatConversationId");
        b.Property(c => c.TenantId).IsRequired();
        // 32 hex + ':' + 32 hex for a direct chat; null once group conversations arrive.
        b.Property(c => c.PairKey).HasMaxLength(65);
        b.Property(c => c.CreatedBy).IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.LastMessageAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(c => c.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        // The pair key is what makes "open a chat with X" idempotent. Group rows carry null, and
        // PostgreSQL treats nulls as distinct, so many of them coexist under a unique index.
        b.HasIndex(c => new { c.TenantId, c.PairKey }).IsUnique();
        b.HasIndex(c => new { c.TenantId, c.LastMessageAt });
    }
}

internal sealed class ChatParticipantConfiguration : IEntityTypeConfiguration<ChatParticipant>
{
    public void Configure(EntityTypeBuilder<ChatParticipant> b)
    {
        b.ToTable("ChatParticipant");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("ChatParticipantId");
        b.Property(p => p.TenantId).IsRequired();
        b.Property(p => p.ConversationId).IsRequired();
        b.Property(p => p.UserId).IsRequired();
        b.Property(p => p.JoinedAt).IsRequired();
        b.Property(p => p.LastReadAt);
        b.Property(p => p.LeftAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ChatConversation>().WithMany().HasForeignKey(p => p.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(p => new { p.ConversationId, p.UserId }).IsUnique();
        b.HasIndex(p => new { p.TenantId, p.UserId });
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("ChatMessage", t =>
        {
            // A live message always has words; a deleted one never does.
            t.HasCheckConstraint("CK_ChatMessage_Body",
                "(\"DeletedAt\" IS NULL AND length(btrim(\"Body\")) > 0) OR (\"DeletedAt\" IS NOT NULL)");
        });

        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("ChatMessageId");
        b.Property(m => m.TenantId).IsRequired();
        b.Property(m => m.ConversationId).IsRequired();
        b.Property(m => m.SenderId).IsRequired();
        b.Property(m => m.Body).HasMaxLength(ChatMessage.MaxBodyLength).IsRequired();
        b.Property(m => m.CreatedAt).IsRequired();
        b.Property(m => m.EditedAt);
        b.Property(m => m.DeletedAt);
        // Replay protection for Idempotency-Key, kept out of the domain entity and out of the API.
        b.Property<string?>("Fingerprint").HasMaxLength(64);

        b.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ChatConversation>().WithMany().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        b.HasIndex("ConversationId", "SenderId", "Fingerprint").IsUnique();
    }
}
