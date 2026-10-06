using BizFlow.Domain.Sla;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class SlaSnapshotConfiguration : IEntityTypeConfiguration<BusinessCalendar>, IEntityTypeConfiguration<SlaVersion>
{
    public void Configure(EntityTypeBuilder<BusinessCalendar> b)
    {
        b.ToTable("BusinessCalendar", t =>
        {
            t.HasCheckConstraint("CK_BusinessCalendar_Hours", "jsonb_typeof(\"WorkingHoursJson\") = 'object'");
            t.HasCheckConstraint("CK_BusinessCalendar_Holidays", "jsonb_typeof(\"HolidaysJson\") = 'array'");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("CalendarId");
        b.Property(x => x.TenantId).IsConcurrencyToken(); b.Property(x => x.TimeZone).HasMaxLength(64);
        b.Property(x => x.WorkingHoursJson).HasColumnType("jsonb"); b.Property(x => x.HolidaysJson).HasColumnType("jsonb");
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }
    public void Configure(EntityTypeBuilder<SlaVersion> b)
    {
        b.ToTable("SLAVersion", t =>
        {
            t.HasCheckConstraint("CK_SLAVersion_Number", "\"VersionNo\" > 0");
            t.HasCheckConstraint("CK_SLAVersion_Thresholds", "\"TargetMinutes\" > 0 AND \"WarningMinutes\" >= 0 AND \"WarningMinutes\" < \"TargetMinutes\"");
            t.HasCheckConstraint("CK_SLAVersion_Escalation", "\"EscalationConfigJson\" IS NULL OR jsonb_typeof(\"EscalationConfigJson\") = 'object'");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("SLAVersionId");
        b.Property(x => x.SlaProfileId).HasColumnName("SLAProfileId").IsConcurrencyToken();
        b.HasOne<SlaProfile>().WithMany().HasForeignKey(x => x.SlaProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BusinessCalendar>().WithMany().HasForeignKey(x => x.CalendarId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SlaProfileId, x.VersionNo }).IsUnique();
        b.Property(x => x.EscalationConfigJson).HasColumnType("jsonb"); b.Property<uint>("Version").IsRowVersion();
    }
}
