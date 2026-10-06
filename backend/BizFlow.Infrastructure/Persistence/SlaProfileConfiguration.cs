using BizFlow.Domain.Sla;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class SlaProfileConfiguration : IEntityTypeConfiguration<SlaProfile>
{
    public void Configure(EntityTypeBuilder<SlaProfile> b)
    {
        b.ToTable("SLAProfile", t => t.HasCheckConstraint("CK_SLAProfile_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200"));
        b.HasKey(p => p.Id); b.Property(p => p.Id).HasColumnName("SLAProfileId");
        b.Property(p => p.TenantId).IsConcurrencyToken(); b.Property(p => p.Name).HasMaxLength(200);
        b.HasIndex(p => new { p.TenantId, p.Status, p.Name });
        b.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }
}
