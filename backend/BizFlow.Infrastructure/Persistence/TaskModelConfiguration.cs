using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class TaskModelConfiguration : IEntityTypeConfiguration<WorkTask>, IEntityTypeConfiguration<TaskChecklistItem>
{
    public void Configure(EntityTypeBuilder<WorkTask> b)
    {
        b.ToTable("Task", t => t.HasCheckConstraint("CK_Task_Title", "length(btrim(\"Title\")) BETWEEN 1 AND 300"));
        b.HasKey(t => t.Id); b.Property(t => t.Id).HasColumnName("TaskId");
        b.Ignore(t => t.WorkflowMutation);
        b.Property(t => t.TenantId).IsConcurrencyToken(); b.Property(t => t.Title).HasMaxLength(300);
        b.Property(t => t.Priority).HasDefaultValue(TaskPriority.Medium).HasSentinel(TaskPriority.Medium);
        b.Property(t => t.Status).HasDefaultValue(TaskState.Draft);
        b.Property(t => t.SlaVersionId).HasColumnName("SLAVersionId");
        b.HasOne<Tenant>().WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(t => t.CreatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(t => t.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(t => t.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SlaVersion>().WithMany().HasForeignKey(t => t.SlaVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => new { t.TenantId, t.Status, t.Deadline });
        b.HasIndex(t => new { t.TenantId, t.CreatorId, t.CreatedAt });
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<TaskChecklistItem> b)
    {
        b.ToTable("TaskChecklistItem", t =>
        {
            t.HasCheckConstraint("CK_TaskChecklistItem_Title", "length(btrim(\"Title\")) BETWEEN 1 AND 300");
            t.HasCheckConstraint("CK_TaskChecklistItem_Order", "\"SortOrder\" > 0");
            t.HasCheckConstraint("CK_TaskChecklistItem_Completion", "(\"IsCompleted\" AND \"CompletedBy\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"CompletedBy\" IS NULL AND \"CompletedAt\" IS NULL)");
        });
        b.HasKey(i => i.Id); b.Property(i => i.Id).HasColumnName("ChecklistItemId");
        b.Property(i => i.Title).HasMaxLength(300); b.Property(i => i.SortOrder).HasDefaultValue(1);
        b.Property(i => i.IsCompleted).HasDefaultValue(false);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(i => i.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(i => i.CompletedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => new { i.TaskId, i.SortOrder });
    }
}
