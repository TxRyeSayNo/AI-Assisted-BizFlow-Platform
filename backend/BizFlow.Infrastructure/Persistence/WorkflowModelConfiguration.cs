using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class WorkflowModelConfiguration : IEntityTypeConfiguration<WorkflowDefinition>,
    IEntityTypeConfiguration<WorkflowVersion>, IEntityTypeConfiguration<WorkflowStep>, IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> b)
    {
        b.ToTable("Workflow"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("WorkflowId");
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.TenantId).IsConcurrencyToken();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<WorkflowVersion> b)
    {
        b.ToTable("WorkflowVersion", table =>
        {
            table.HasCheckConstraint("CK_WorkflowVersion_Number", "\"VersionNo\" > 0");
            table.HasCheckConstraint("CK_WorkflowVersion_Publication", "(\"Status\" = 'DRAFT' AND \"PublishedAt\" IS NULL) OR (\"Status\" <> 'DRAFT' AND \"PublishedAt\" IS NOT NULL)");
            table.HasCheckConstraint("CK_WorkflowVersion_JsonObject", "jsonb_typeof(\"DefinitionJson\") = 'object'");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("WorkflowVersionId");
        b.Property(x => x.WorkflowId).IsConcurrencyToken();
        b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.WorkflowId, x.VersionNo }).IsUnique();
        b.Property(x => x.DefinitionJson).HasColumnType("jsonb"); b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<WorkflowStep> b)
    {
        b.ToTable("WorkflowStep", table =>
        {
            table.HasCheckConstraint("CK_WorkflowStep_Order", "\"OrderNo\" > 0");
            table.HasCheckConstraint("CK_WorkflowStep_Code", "\"StepCode\" ~ '^[A-Z0-9_.-]{1,80}$'");
            table.HasCheckConstraint("CK_WorkflowStep_JsonObject", "jsonb_typeof(\"ConfigJson\") = 'object'");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("WorkflowStepId");
        b.Property(x => x.WorkflowVersionId).IsConcurrencyToken();
        b.Property(x => x.StepCode).HasMaxLength(80); b.Property(x => x.Name).HasMaxLength(200);
        b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(x => x.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.WorkflowVersionId, x.StepCode }).IsUnique();
        b.HasIndex(x => new { x.WorkflowVersionId, x.OrderNo }).IsUnique();
        b.Property(x => x.ConfigJson).HasColumnType("jsonb"); b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<WorkflowTransition> b)
    {
        b.ToTable("WorkflowTransition", table => table.HasCheckConstraint("CK_WorkflowTransition_JsonObject", "\"GuardJson\" IS NULL OR jsonb_typeof(\"GuardJson\") = 'object'"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("TransitionId");
        b.Property(x => x.WorkflowVersionId).IsConcurrencyToken();
        b.Property(x => x.FromState).HasMaxLength(50); b.Property(x => x.ToState).HasMaxLength(50);
        b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(x => x.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.GuardJson).HasColumnType("jsonb"); b.Property<uint>("Version").IsRowVersion();
    }
}
