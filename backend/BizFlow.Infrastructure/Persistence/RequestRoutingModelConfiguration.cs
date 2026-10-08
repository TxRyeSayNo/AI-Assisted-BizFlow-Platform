using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class RequestRoutingModelConfiguration : IEntityTypeConfiguration<RequestRouting>
{
    public void Configure(EntityTypeBuilder<RequestRouting> b)
    {
        b.ToTable("RequestRouting", t =>
        {
            t.HasCheckConstraint("CK_RequestRouting_Source", "\"Source\" IN ('MANUAL', 'AI', 'RULE')");
        });
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("RequestRoutingId");
        b.Property(r => r.Source)
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => Enum.Parse<RequestRoutingSource>(s, true))
            .HasMaxLength(20);
        b.Property(r => r.Reason).HasMaxLength(2000);
        b.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(r => r.FromDepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(r => r.ToDepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.FromUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.ToUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.RoutedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.TenantId, r.RequestId, r.RoutedAt });
    }
}
