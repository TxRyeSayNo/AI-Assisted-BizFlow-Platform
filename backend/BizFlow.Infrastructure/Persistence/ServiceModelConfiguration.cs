using BizFlow.Domain.Services;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class ServiceModelConfiguration : IEntityTypeConfiguration<InternalService>, IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<InternalService> b)
    {
        b.ToTable("Service", t =>
        {
            t.HasCheckConstraint("CK_Service_Code", "length(btrim(\"Code\")) BETWEEN 1 AND 80");
            t.HasCheckConstraint("CK_Service_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
        });
        b.HasKey(s => s.Id); b.Property(s => s.Id).HasColumnName("ServiceId");
        b.Property(s => s.TenantId).IsConcurrencyToken(); b.Property(s => s.Code).HasMaxLength(80); b.Property(s => s.Name).HasMaxLength(200);
        b.Property(s => s.ActiveSlaVersionId).HasColumnName("ActiveSLAVersionId");
        b.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(s => s.ActiveWorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SlaVersion>().WithMany().HasForeignKey(s => s.ActiveSlaVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.TenantId, s.Code }).IsUnique(); b.HasIndex(s => new { s.TenantId, s.Status, s.Name });
        b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<ServiceCategory> b)
    {
        b.ToTable("ServiceCategory", t =>
        {
            t.HasCheckConstraint("CK_ServiceCategory_Code", "length(btrim(\"Code\")) BETWEEN 1 AND 80");
            t.HasCheckConstraint("CK_ServiceCategory_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
        });
        b.HasKey(c => c.Id); b.Property(c => c.Id).HasColumnName("ServiceCategoryId");
        b.Property(c => c.ServiceId).IsConcurrencyToken(); b.Property(c => c.Code).HasMaxLength(80); b.Property(c => c.Name).HasMaxLength(200);
        b.HasOne<InternalService>().WithMany().HasForeignKey(c => c.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.ServiceId, c.Code }).IsUnique(); b.Property<uint>("Version").IsRowVersion();
    }
}
