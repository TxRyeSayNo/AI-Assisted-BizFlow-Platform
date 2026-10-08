using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class AttachmentModelConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("Attachment", t =>
        {
            t.HasCheckConstraint("CK_Attachment_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST','COMMENT','RESULT','PROGRESS')");
            t.HasCheckConstraint("CK_Attachment_Status", "\"Status\" IN ('UPLOADING','READY','FAILED','DELETED')");
            t.HasCheckConstraint("CK_Attachment_SizeBytes", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 524288000");
        });

        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("AttachmentId");
        b.Property(a => a.TenantId).IsRequired();
        b.Property(a => a.ObjectType)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AttachmentObjectType>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.ObjectId).IsRequired();
        b.Property(a => a.UploadedBy).IsRequired();
        b.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        b.Property(a => a.SizeBytes).IsRequired();
        b.Property(a => a.ObjectKey).HasMaxLength(500).IsRequired();
        b.Property(a => a.Hash).HasMaxLength(128).IsRequired();
        b.Property(a => a.Status)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AttachmentStatus>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.CreatedAt).IsRequired();
        b.Property(a => a.DeletedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.UploadedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(a => new { a.TenantId, a.ObjectType, a.ObjectId, a.CreatedAt });
        b.HasIndex(a => new { a.TenantId, a.ObjectKey }).IsUnique();
        b.HasIndex(a => a.UploadedBy);
    }
}
