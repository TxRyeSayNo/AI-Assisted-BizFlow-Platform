using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class ConfirmationConfiguration : IEntityTypeConfiguration<Confirmation>
{
    public void Configure(EntityTypeBuilder<Confirmation> b)
    {
        b.ToTable("Confirmation", t =>
        {
            t.HasCheckConstraint("CK_Confirmation_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
            t.HasCheckConstraint("CK_Confirmation_Milestone", "\"MilestoneType\" IN ('RECEIVE','RESULT','RESOLUTION')");
            t.HasCheckConstraint("CK_Confirmation_Decision", "\"Decision\" IN ('CONFIRMED','REJECTED')");
        });
        b.HasKey(c => c.Id); b.Property(c => c.Id).HasColumnName("ConfirmationId");
        b.Property(c => c.ObjectType).HasDefaultValue("TASK");
        b.Property(c => c.MilestoneType).HasDefaultValue("RESULT");
        b.Property(c => c.Decision).HasDefaultValue("CONFIRMED");
        b.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(c => c.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.TenantId, c.ObjectType, c.ObjectId, c.ConfirmedAt });
    }
}
