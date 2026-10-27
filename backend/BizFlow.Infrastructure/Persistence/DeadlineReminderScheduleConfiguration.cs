using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class DeadlineReminderScheduleConfiguration : IEntityTypeConfiguration<DeadlineReminderSchedule>
{
    public void Configure(EntityTypeBuilder<DeadlineReminderSchedule> b)
    {
        b.ToTable("DeadlineReminderSchedule");
        b.HasKey(x => x.Id);
        b.Property(x => x.TriggerType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();

        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.TaskId, x.UserId, x.TriggerType }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Status, x.RemindAt });
    }
}
