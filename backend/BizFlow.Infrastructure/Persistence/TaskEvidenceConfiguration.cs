using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class TaskEvidenceConfiguration : IEntityTypeConfiguration<TaskProgressReport>, IEntityTypeConfiguration<TaskResult>
{
    public void Configure(EntityTypeBuilder<TaskProgressReport> b)
    {
        b.ToTable("TaskProgressReport", t => t.HasCheckConstraint("CK_TaskProgressReport_Percent", "\"Percent\" BETWEEN 0 AND 100"));
        b.HasKey(r => r.Id); b.Property(r => r.Id).HasColumnName("ProgressReportId");
        b.Property(r => r.Percent).HasDefaultValue((short)0);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(r => r.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.TaskId, r.SubmittedAt });
    }

    public void Configure(EntityTypeBuilder<TaskResult> b)
    {
        b.ToTable("TaskResult", t => t.HasCheckConstraint("CK_TaskResult_Revision", "\"RevisionNo\" > 0"));
        b.HasKey(r => r.Id); b.Property(r => r.Id).HasColumnName("TaskResultId");
        b.Property(r => r.RevisionNo).HasDefaultValue(1);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(r => r.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.TaskId, r.RevisionNo });
    }
}
