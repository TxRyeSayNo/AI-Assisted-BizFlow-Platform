using BizFlow.Domain.Requests;
using BizFlow.Domain.Workflows;
using BizFlow.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class WorkflowRuntimeConfiguration : IEntityTypeConfiguration<WorkflowInstance>,
    IEntityTypeConfiguration<WorkflowStepExecution>, IEntityTypeConfiguration<WorkflowDecision>, IEntityTypeConfiguration<WorkflowParticipant>,
    IEntityTypeConfiguration<WorkflowDelegation>, IEntityTypeConfiguration<WorkflowSlaPause>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> b)
    {
        b.ToTable("WorkflowInstance"); b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TenantId, x.RequestId }).IsUnique();
        b.HasOne<WorkRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(x => x.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<WorkflowStepExecution> b)
    {
        b.ToTable("WorkflowStepExecution"); b.HasKey(x => x.Id);
        b.Property(x => x.NodeId).HasMaxLength(100); b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.NodeType).HasMaxLength(30); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Policy).HasMaxLength(10); b.Property(x => x.Outcome).HasMaxLength(50);
        b.Property(x => x.CandidatesJson).HasColumnType("jsonb");
        b.HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.InstanceId, x.StartedAt }); b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<WorkflowDecision> b)
    {
        b.ToTable("WorkflowDecision"); b.HasKey(x => x.Id);
        b.Property(x => x.Outcome).HasMaxLength(50); b.Property(x => x.Note).HasMaxLength(4000);
        b.HasOne<WorkflowStepExecution>().WithMany().HasForeignKey(x => x.StepExecutionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PrincipalId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.StepExecutionId, x.ActorId }).IsUnique();
        b.HasIndex(x => new { x.StepExecutionId, x.PrincipalId }).IsUnique();
    }
    public void Configure(EntityTypeBuilder<WorkflowParticipant> b)
    {
        b.ToTable("WorkflowParticipant"); b.HasKey(x => new { x.StepExecutionId, x.UserId });
        b.HasOne<WorkflowStepExecution>().WithMany().HasForeignKey(x => x.StepExecutionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.InstanceId });
    }
    public void Configure(EntityTypeBuilder<WorkflowDelegation> b)
    {
        b.ToTable("WorkflowDelegation"); b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasOne<WorkflowStepExecution>().WithMany().HasForeignKey(x => x.StepExecutionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PrincipalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.DelegateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.StepExecutionId, x.PrincipalId }).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        b.HasIndex(x => new { x.StepExecutionId, x.DelegateId }).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        b.HasIndex(x => new { x.TenantId, x.DelegateId, x.ExpiresAt });
    }
    public void Configure(EntityTypeBuilder<WorkflowSlaPause> b)
    {
        b.ToTable("WorkflowSlaPause"); b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.CalendarSnapshotJson).HasColumnType("jsonb");
        b.HasOne<WorkflowStepExecution>().WithMany().HasForeignKey(x => x.StepExecutionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PausedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ResumedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.StepExecutionId).IsUnique().HasFilter("\"ResumedAt\" IS NULL");
        b.HasIndex(x => new { x.TenantId, x.InstanceId, x.PausedAt });
    }
}
