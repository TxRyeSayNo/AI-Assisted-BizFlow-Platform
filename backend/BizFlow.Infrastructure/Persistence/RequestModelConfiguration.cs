using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class RequestModelConfiguration : IEntityTypeConfiguration<WorkRequest>
{
    public void Configure(EntityTypeBuilder<WorkRequest> b)
    {
        b.ToTable("Request", t =>
        {
            t.HasCheckConstraint("CK_Request_Title", "length(btrim(\"Title\")) BETWEEN 1 AND 300");
            t.HasCheckConstraint("CK_Request_Description", "length(btrim(\"Description\")) > 0");
            t.HasCheckConstraint("CK_Request_Parent", "\"ParentRequestId\" IS NULL OR \"ParentRequestId\" <> \"RequestId\"");
            t.HasCheckConstraint("CK_Request_Revision", "\"RevisedFromRequestId\" IS NULL OR \"RevisedFromRequestId\" <> \"RequestId\"");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).HasColumnName("RequestId");
        b.Ignore(r => r.WorkflowMutation);
        b.Property(r => r.TenantId).IsConcurrencyToken(); b.Property(r => r.Title).HasMaxLength(300);
        // LOW is enum zero and must not be mistaken for an unset value.
        b.Property(r => r.Priority).HasDefaultValue(RequestPriority.Medium).HasSentinel(RequestPriority.Medium);
        b.Property(r => r.Status).HasDefaultValue(RequestState.Draft);
        b.Property(r => r.SlaVersionId).HasColumnName("SLAVersionId");
        b.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InternalService>().WithMany().HasForeignKey(r => r.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceCategory>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(r => r.ParentRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(r => r.RevisedFromRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(r => r.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SlaVersion>().WithMany().HasForeignKey(r => r.SlaVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.TenantId, r.Status, r.CreatedAt });
        b.HasIndex(r => new { r.TenantId, r.RequesterId, r.CreatedAt });
        b.Property<uint>("Version").IsRowVersion();
    }
}
