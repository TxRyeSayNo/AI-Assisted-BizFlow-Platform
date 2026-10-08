using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class CommentModelConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("Comment", t =>
        {
            t.HasCheckConstraint("CK_Comment_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST','RESULT','PROGRESS')");
        });

        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("CommentId");
        b.Property(c => c.TenantId).IsRequired();
        b.Property(c => c.ObjectType)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<CommentObjectType>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(c => c.ObjectId).IsRequired();
        b.Property(c => c.AuthorId).IsRequired();
        b.Property(c => c.Content).IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.EditedAt);
        b.Property(c => c.DeletedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(c => new { c.TenantId, c.ObjectType, c.ObjectId, c.CreatedAt });
        b.HasIndex(c => c.AuthorId);
    }
}
