using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class WebPushSubscriptionConfiguration : IEntityTypeConfiguration<WebPushSubscription>
{
    public void Configure(EntityTypeBuilder<WebPushSubscription> b)
    {
        b.ToTable("WebPushSubscription");
        b.HasKey(x => x.Id);
        b.Property(x => x.Endpoint).HasMaxLength(1000).IsRequired();
        b.Property(x => x.P256dh).HasMaxLength(250).IsRequired();
        b.Property(x => x.Auth).HasMaxLength(150).IsRequired();

        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.UserId, x.Endpoint }).IsUnique();
    }
}
