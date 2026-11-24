using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

/// <summary>F7 storage: which task waits for which other task.</summary>
internal sealed class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> b)
    {
        b.ToTable("TaskDependency", t =>
        {
            t.HasCheckConstraint("CK_TaskDependency_NotSelf", "\"TaskId\" <> \"DependsOnTaskId\"");
        });

        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("TaskDependencyId");
        b.Property(d => d.TenantId).IsRequired();
        b.Property(d => d.TaskId).IsRequired();
        b.Property(d => d.DependsOnTaskId).IsRequired();
        b.Property(d => d.CreatedBy).IsRequired();
        b.Property(d => d.CreatedAt).IsRequired();
        b.Property(d => d.RemovedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(d => d.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkTask>().WithMany().HasForeignKey(d => d.DependsOnTaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(d => d.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        // One row per pair; unlinking flips RemovedAt rather than deleting it.
        b.HasIndex(d => new { d.TenantId, d.TaskId, d.DependsOnTaskId }).IsUnique();
        // Reading the other direction — what waits on this task — is just as common.
        b.HasIndex(d => new { d.TenantId, d.DependsOnTaskId, d.RemovedAt });
    }
}
