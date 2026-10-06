using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> b)
    {
        b.ToTable("TenantSetting", t => t.HasCheckConstraint("CK_TenantSetting_Value", "bizflow_valid_tenant_setting(\"Key\", \"ValueJson\")"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("TenantSettingId");
        b.Property(x => x.TenantId).IsConcurrencyToken();
        b.Property(x => x.Key).HasMaxLength(128).IsConcurrencyToken();
        b.Property(x => x.ValueJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }
}
