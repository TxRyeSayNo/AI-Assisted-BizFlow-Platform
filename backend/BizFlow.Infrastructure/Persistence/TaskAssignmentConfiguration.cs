using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> b)
    {
        b.ToTable("TaskAssignment", t =>
        {
            t.HasCheckConstraint("CK_TaskAssignment_Target", "\"DepartmentId\" IS NOT NULL OR \"UserId\" IS NOT NULL");
            t.HasCheckConstraint("CK_TaskAssignment_Receipt", "NOT (\"AcceptedAt\" IS NOT NULL AND \"RejectedAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_TaskAssignment_Reason", "(\"RejectedAt\" IS NULL AND \"RejectionReason\" IS NULL) OR (\"RejectedAt\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL AND length(btrim(\"RejectionReason\")) > 0)");
            t.HasCheckConstraint("CK_TaskAssignment_Times", "(\"AcceptedAt\" IS NULL OR \"AcceptedAt\" >= \"AssignedAt\") AND (\"RejectedAt\" IS NULL OR \"RejectedAt\" >= \"AssignedAt\") AND (\"EndedAt\" IS NULL OR (\"EndedAt\" >= \"AssignedAt\" AND (\"AcceptedAt\" IS NULL OR \"AcceptedAt\" <= \"EndedAt\") AND (\"RejectedAt\" IS NULL OR \"RejectedAt\" <= \"EndedAt\")))");
        });
        b.HasKey(a => a.Id); b.Property(a => a.Id).HasColumnName("TaskAssignmentId");
        b.HasOne<WorkTask>().WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => a.TaskId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
        b.HasIndex(a => new { a.TaskId, a.AssignedAt });
    }
}
