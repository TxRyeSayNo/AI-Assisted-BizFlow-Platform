using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class RequestResolutionConfiguration : IEntityTypeConfiguration<RequestResolution>
{
    public void Configure(EntityTypeBuilder<RequestResolution> b)
    {
        b.ToTable("RequestResolution", t =>
        {
            t.HasCheckConstraint("CK_RequestResolution_Content", "length(btrim(\"Content\")) > 0");
            t.HasCheckConstraint("CK_RequestResolution_Revision", "\"RevisionNo\" > 0");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).HasColumnName("ResolutionId");
        b.Property(r => r.RevisionNo).HasDefaultValue(1);
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.ResolverId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.RequestId, r.RevisionNo });
    }
}
