using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notification", table =>
        {
            table.HasCheckConstraint("CK_Notification_Type", "\"Type\" IN ('TaskAssigned','TaskAccepted','TaskRejected','ProgressSubmitted','ResultSubmitted','TaskConfirmed','RequestSubmitted','RequestRouted','RequestReceived','RequestResolved','RequestRejected','RequestRevised','DeadlineWarning','Overdue','Escalation','ApprovalRequired')");
            table.HasCheckConstraint("CK_Notification_Text", "length(btrim(\"Title\")) > 0 AND length(btrim(\"Content\")) > 0 AND length(btrim(\"IdempotencyKey\")) > 0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("NotificationId");
        b.Property(x => x.TenantId).IsConcurrencyToken(); b.Property(x => x.RecipientId).IsConcurrencyToken();
        b.Property(x => x.Type).HasMaxLength(80); b.Property(x => x.ObjectType).HasMaxLength(40);
        b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.IdempotencyKey).HasMaxLength(180);
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.RecipientId, x.Id });
        b.HasIndex(x => new { x.TenantId, x.RecipientId, x.ReadAt });
        b.Property<uint>("Version").IsRowVersion();
    }
}
