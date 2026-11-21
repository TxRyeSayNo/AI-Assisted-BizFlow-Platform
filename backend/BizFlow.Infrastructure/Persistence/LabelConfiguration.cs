using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

/// <summary>F6 storage: the per-tenant label catalogue and the labels carried by work items.</summary>
internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> b)
    {
        b.ToTable("Label", t =>
        {
            t.HasCheckConstraint("CK_Label_Colour", "\"Colour\" ~ '^#[0-9A-F]{6}$'");
            t.HasCheckConstraint("CK_Label_Name", "length(btrim(\"Name\")) > 0");
        });

        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("LabelId");
        b.Property(l => l.TenantId).IsRequired();
        b.Property(l => l.Name).HasMaxLength(Label.MaxNameLength).IsRequired();
        b.Property(l => l.NormalizedName).HasMaxLength(Label.MaxNameLength).IsRequired();
        b.Property(l => l.Colour).HasMaxLength(7).IsRequired();
        b.Property(l => l.CreatedBy).IsRequired();
        b.Property(l => l.CreatedAt).IsRequired();
        b.Property(l => l.UpdatedAt);
        b.Property(l => l.ArchivedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        // One name per workspace, archived or not, so reusing a retired name is a deliberate restore.
        b.HasIndex(l => new { l.TenantId, l.NormalizedName }).IsUnique();
    }
}

internal sealed class WorkItemLabelConfiguration : IEntityTypeConfiguration<WorkItemLabel>
{
    public void Configure(EntityTypeBuilder<WorkItemLabel> b)
    {
        b.ToTable("WorkItemLabel", t =>
        {
            t.HasCheckConstraint("CK_WorkItemLabel_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
        });

        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("WorkItemLabelId");
        b.Property(a => a.TenantId).IsRequired();
        b.Property(a => a.ObjectType)
            .HasConversion(v => v.ToString().ToUpperInvariant(), v => Enum.Parse<CommentObjectType>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.ObjectId).IsRequired();
        b.Property(a => a.LabelId).IsRequired();
        b.Property(a => a.CreatedBy).IsRequired();
        b.Property(a => a.CreatedAt).IsRequired();
        b.Property(a => a.RemovedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Label>().WithMany().HasForeignKey(a => a.LabelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        // One row per label per item; detaching flips RemovedAt rather than deleting it.
        b.HasIndex(a => new { a.TenantId, a.ObjectType, a.ObjectId, a.LabelId }).IsUnique();
        b.HasIndex(a => new { a.TenantId, a.LabelId, a.RemovedAt });
    }
}
