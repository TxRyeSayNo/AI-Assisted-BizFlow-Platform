using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class RequestEscalationModelConfiguration : IEntityTypeConfiguration<RequestEscalation>
{
    public void Configure(EntityTypeBuilder<RequestEscalation> b)
    {
        b.ToTable("RequestEscalation", t =>
        {
            t.HasCheckConstraint("CK_RequestEscalation_State", "\"State\" IN ('PENDING', 'APPROVED', 'REJECTED')");
            t.HasCheckConstraint("CK_RequestEscalation_Reason", "length(btrim(\"Reason\")) > 0");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("RequestEscalationId");
        b.Property(e => e.State)
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => Enum.Parse<RequestEscalationState>(s, true))
            .HasMaxLength(20);
        b.Property(e => e.Reason).HasMaxLength(2000);
        b.Property(e => e.DecisionNote).HasMaxLength(2000);
        b.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(e => e.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.EscalatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.DecidedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.TenantId, e.RequestId, e.EscalatedAt });
        b.HasIndex(e => new { e.TenantId, e.State });
    }
}
