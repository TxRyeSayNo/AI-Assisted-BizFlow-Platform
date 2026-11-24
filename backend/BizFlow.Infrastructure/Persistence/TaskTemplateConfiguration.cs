using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

/// <summary>F8/F9 storage: the task template catalogue, with its optional recurrence on the row.</summary>
internal sealed class TaskTemplateConfiguration : IEntityTypeConfiguration<TaskTemplate>
{
    public void Configure(EntityTypeBuilder<TaskTemplate> b)
    {
        b.ToTable("TaskTemplate", t =>
        {
            t.HasCheckConstraint("CK_TaskTemplate_Name", "length(btrim(\"Name\")) > 0");
            t.HasCheckConstraint("CK_TaskTemplate_Recurrence",
                "\"RecurrenceFrequency\" IS NULL OR \"RecurrenceFrequency\" IN ('DAILY','WEEKLY','MONTHLY')");
            // A recurrence always carries an hour; a weekly or monthly one also carries a day.
            t.HasCheckConstraint("CK_TaskTemplate_RecurrenceShape", """
                ("RecurrenceFrequency" IS NULL AND "RecurrenceDay" IS NULL AND "RecurrenceHourUtc" IS NULL
                   AND "NextRunAt" IS NULL)
                OR ("RecurrenceFrequency" = 'DAILY' AND "RecurrenceDay" IS NULL
                   AND "RecurrenceHourUtc" BETWEEN 0 AND 23)
                OR ("RecurrenceFrequency" = 'WEEKLY' AND "RecurrenceDay" BETWEEN 1 AND 7
                   AND "RecurrenceHourUtc" BETWEEN 0 AND 23)
                OR ("RecurrenceFrequency" = 'MONTHLY' AND "RecurrenceDay" BETWEEN 1 AND 28
                   AND "RecurrenceHourUtc" BETWEEN 0 AND 23)
                """);
            t.HasCheckConstraint("CK_TaskTemplate_DeadlineOffset",
                "\"DeadlineOffsetDays\" IS NULL OR \"DeadlineOffsetDays\" BETWEEN 0 AND 365");
        });

        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("TaskTemplateId");
        b.Property(t => t.TenantId).IsRequired();
        b.Property(t => t.Name).HasMaxLength(TaskTemplate.MaxNameLength).IsRequired();
        b.Property(t => t.NormalizedName).HasMaxLength(TaskTemplate.MaxNameLength).IsRequired();
        b.Property(t => t.TitleTemplate).HasMaxLength(TaskTemplate.MaxTitleLength).IsRequired();
        b.Property(t => t.Description).HasMaxLength(TaskTemplate.MaxDescriptionLength);
        b.Property(t => t.Priority).HasMaxLength(32);
        b.Property(t => t.DeadlineOffsetDays);
        b.Property(t => t.RecurrenceFrequency).HasMaxLength(16);
        b.Property(t => t.RecurrenceDay);
        b.Property(t => t.RecurrenceHourUtc);
        b.Property(t => t.NextRunAt);
        b.Property(t => t.LastRunAt);
        b.Property(t => t.CreatedBy).IsRequired();
        b.Property(t => t.CreatedAt).IsRequired();
        b.Property(t => t.UpdatedAt);
        b.Property(t => t.ArchivedAt);
        b.Ignore(t => t.Repeats);

        b.HasOne<Tenant>().WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(t => t.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        // One name per workspace, archived or not, so reusing a retired name is a deliberate restore.
        b.HasIndex(t => new { t.TenantId, t.NormalizedName }).IsUnique();
        // The recurring job scans only templates that are actually due.
        b.HasIndex(t => t.NextRunAt).HasFilter("\"NextRunAt\" IS NOT NULL AND \"ArchivedAt\" IS NULL");
    }
}

/// <summary>F12 storage: saved list filters, private to the person who saved them.</summary>
internal sealed class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> b)
    {
        b.ToTable("SavedFilter", t =>
        {
            t.HasCheckConstraint("CK_SavedFilter_Scope", "\"Scope\" IN ('TASK','REQUEST')");
            t.HasCheckConstraint("CK_SavedFilter_Name", "length(btrim(\"Name\")) > 0");
        });

        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasColumnName("SavedFilterId");
        b.Property(f => f.TenantId).IsRequired();
        b.Property(f => f.UserId).IsRequired();
        b.Property(f => f.Scope).HasMaxLength(16).IsRequired();
        b.Property(f => f.Name).HasMaxLength(SavedFilter.MaxNameLength).IsRequired();
        b.Property(f => f.NormalizedName).HasMaxLength(SavedFilter.MaxNameLength).IsRequired();
        b.Property(f => f.Query).HasMaxLength(SavedFilter.MaxQueryLength).IsRequired();
        b.Property(f => f.CreatedAt).IsRequired();
        b.Property(f => f.UpdatedAt);
        b.Property(f => f.RemovedAt);

        b.HasOne<Tenant>().WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);

        // One name per person per list, removed rows included, so deleting and re-saving reuses the row.
        b.HasIndex(f => new { f.TenantId, f.UserId, f.Scope, f.NormalizedName }).IsUnique();
    }
}
