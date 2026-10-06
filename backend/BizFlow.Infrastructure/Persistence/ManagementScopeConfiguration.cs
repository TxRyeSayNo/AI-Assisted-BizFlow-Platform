using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class ManagementScopeConfiguration : IEntityTypeConfiguration<ManagementScope>
{
    public void Configure(EntityTypeBuilder<ManagementScope> b)
    {
        b.ToTable("ManagementScope"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ManagementScopeId");
        b.Property(x => x.TenantId).IsConcurrencyToken();
        b.HasIndex(x => new { x.TenantId, x.UserId, x.DepartmentId }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => new { x.TenantId, x.DepartmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        // Tenant matching for nullable-plane User keys is additionally enforced by a SQL trigger.
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }
}
