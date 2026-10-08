using System;
using BizFlow.Domain.AI;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class AIInteractionModelConfiguration : IEntityTypeConfiguration<AIInteraction>
{
    public void Configure(EntityTypeBuilder<AIInteraction> b)
    {
        b.ToTable("AIInteraction", t =>
        {
            t.HasCheckConstraint("CK_AIInteraction_Status", "\"Status\" IN ('SUCCEEDED','FAILED','TIMEOUT','REJECTED')");
        });

        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("AIInteractionId");
        b.Property(i => i.TenantId).IsRequired();
        b.Property(i => i.UserId).IsRequired();
        b.Property(i => i.Feature).HasMaxLength(80).IsRequired();
        b.Property(i => i.ModelName).HasMaxLength(120).IsRequired();
        b.Property(i => i.InputRef).HasMaxLength(255);
        b.Property(i => i.OutputRef).HasMaxLength(255);
        b.Property(i => i.Status)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AIInteractionStatus>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(i => i.LatencyMs);
        b.Property(i => i.TokenUsage).HasColumnType("jsonb");
        b.Property(i => i.CreatedAt).IsRequired();

        b.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(i => new { i.TenantId, i.CreatedAt });
        b.HasIndex(i => new { i.TenantId, i.UserId });
    }
}

internal sealed class AIRecommendationModelConfiguration : IEntityTypeConfiguration<AIRecommendation>
{
    public void Configure(EntityTypeBuilder<AIRecommendation> b)
    {
        b.ToTable("AIRecommendation", t =>
        {
            t.HasCheckConstraint("CK_AIRecommendation_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
            t.HasCheckConstraint("CK_AIRecommendation_HumanDecision", "\"HumanDecision\" IS NULL OR \"HumanDecision\" IN ('ACCEPTED','OVERRIDDEN','DISMISSED')");
        });

        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("RecommendationId");
        b.Property(r => r.TenantId).IsRequired();
        b.Property(r => r.AIInteractionId).IsRequired();
        b.Property(r => r.ObjectType)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<RecommendationObjectType>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(r => r.ObjectId);
        b.Property(r => r.RecommendationType).HasMaxLength(80).IsRequired();
        b.Property(r => r.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(r => r.Confidence).HasPrecision(5, 4);
        b.Property(r => r.HumanDecision)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString().ToUpperInvariant() : null,
                v => string.IsNullOrEmpty(v) ? null : Enum.Parse<HumanDecision>(v, true))
            .HasMaxLength(32);
        b.Property(r => r.DecisionBy);
        b.Property(r => r.DecidedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AIInteraction>().WithMany().HasForeignKey(r => r.AIInteractionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(r => r.DecisionBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(r => new { r.TenantId, r.ObjectType, r.ObjectId });
        b.HasIndex(r => new { r.TenantId, r.AIInteractionId });
    }
}

internal sealed class AIAgentActionModelConfiguration : IEntityTypeConfiguration<AIAgentAction>
{
    public void Configure(EntityTypeBuilder<AIAgentAction> b)
    {
        b.ToTable("AIAgentAction", t =>
        {
            t.HasCheckConstraint("CK_AIAgentAction_AuthorizationResult", "\"AuthorizationResult\" IN ('AUTHORIZED','DENIED')");
            t.HasCheckConstraint("CK_AIAgentAction_ExecutionStatus", "\"ExecutionStatus\" IN ('NOT_EXECUTED','SUCCEEDED','FAILED')");
        });

        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("AIAgentActionId");
        b.Property(a => a.TenantId).IsRequired();
        b.Property(a => a.AIInteractionId).IsRequired();
        b.Property(a => a.ToolName).HasMaxLength(100).IsRequired();
        b.Property(a => a.ArgsJson).HasColumnType("jsonb").IsRequired();
        b.Property(a => a.AuthorizationResult)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AgentAuthorizationResult>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.ExecutionStatus)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AgentExecutionStatus>(v, true))
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.ResultRef).HasMaxLength(255);
        b.Property(a => a.CreatedAt).IsRequired();

        b.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AIInteraction>().WithMany().HasForeignKey(a => a.AIInteractionId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.TenantId, a.CreatedAt });
        b.HasIndex(a => new { a.TenantId, a.AIInteractionId });
    }
}
