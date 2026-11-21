using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

/// <summary>
/// F3 storage: watchers and comment mentions. Both tables are new, carry TenantId and follow the
/// same conversions, constraints and tenant filtering the existing collaboration tables use.
/// </summary>
internal sealed class WorkItemWatcherConfiguration : IEntityTypeConfiguration<WorkItemWatcher>
{
    public void Configure(EntityTypeBuilder<WorkItemWatcher> b)
    {
        b.ToTable("WorkItemWatcher", t =>
        {
            t.HasCheckConstraint("CK_WorkItemWatcher_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
        });

        b.HasKey(w => w.Id);
        b.Property(w => w.Id).HasColumnName("WorkItemWatcherId");
        b.Property(w => w.TenantId).IsRequired();
        b.Property(w => w.ObjectType)
            .HasConversion(v => v.ToString().ToUpperInvariant(), v => Enum.Parse<CommentObjectType>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(w => w.ObjectId).IsRequired();
        b.Property(w => w.UserId).IsRequired();
        b.Property(w => w.CreatedAt).IsRequired();
        b.Property(w => w.LastNotifiedAt);
        b.Property(w => w.RemovedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Restrict);

        // One subscription per person per item; the service relies on this to stay idempotent.
        b.HasIndex(w => new { w.TenantId, w.ObjectType, w.ObjectId, w.UserId }).IsUnique();
        b.HasIndex(w => new { w.TenantId, w.UserId });
    }
}

internal sealed class CommentMentionConfiguration : IEntityTypeConfiguration<CommentMention>
{
    public void Configure(EntityTypeBuilder<CommentMention> b)
    {
        b.ToTable("CommentMention", t =>
        {
            t.HasCheckConstraint("CK_CommentMention_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
        });

        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("CommentMentionId");
        b.Property(m => m.TenantId).IsRequired();
        b.Property(m => m.CommentId).IsRequired();
        b.Property(m => m.ObjectType)
            .HasConversion(v => v.ToString().ToUpperInvariant(), v => Enum.Parse<CommentObjectType>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(m => m.ObjectId).IsRequired();
        b.Property(m => m.MentionedUserId).IsRequired();
        b.Property(m => m.MentionedByUserId).IsRequired();
        b.Property(m => m.CreatedAt).IsRequired();

        b.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Comment>().WithMany().HasForeignKey(m => m.CommentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(m => m.MentionedUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => new { m.CommentId, m.MentionedUserId }).IsUnique();
        b.HasIndex(m => new { m.TenantId, m.MentionedUserId, m.CreatedAt });
    }
}
