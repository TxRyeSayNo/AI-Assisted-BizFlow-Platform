using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> b)
    {
        b.ToTable("NotificationLog");
        b.HasKey(x => x.Id);
        b.Property(x => x.TriggerType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);

        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        // Deduplication unique constraint (ADR-SSS-010 #3, OD-032)
        b.HasIndex(x => new { x.TenantId, x.TaskId, x.UserId, x.TriggerType, x.ScheduledAt }).IsUnique();
    }
}
