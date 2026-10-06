using BizFlow.Domain.Audit;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizFlow.Infrastructure.Persistence;

internal sealed class IdentityModelConfiguration :
    IEntityTypeConfiguration<Company>, IEntityTypeConfiguration<Tenant>, IEntityTypeConfiguration<UserAccount>,
    IEntityTypeConfiguration<Department>, IEntityTypeConfiguration<Role>, IEntityTypeConfiguration<Permission>,
    IEntityTypeConfiguration<UserRole>, IEntityTypeConfiguration<RolePermission>,
    IEntityTypeConfiguration<AuthenticationSession>, IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Company"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("CompanyId");
        b.Property(x => x.Code).HasMaxLength(50); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.ContactEmail).HasMaxLength(320);
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenant"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("TenantId");
        b.Property(x => x.TenantKey).HasMaxLength(80); b.HasIndex(x => x.TenantKey).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.TimeZone).HasMaxLength(64);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CompanyId).IsUnique(); // one primary tenant per company in the MVP
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Department", table => table.HasCheckConstraint("CK_Department_NoSelfParent", "\"ParentDepartmentId\" IS NULL OR \"ParentDepartmentId\" <> \"DepartmentId\""));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("DepartmentId");
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.TenantId).IsConcurrencyToken();
        b.Property(x => x.Code).HasMaxLength(50); b.Property(x => x.Name).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => new { x.TenantId, x.ParentDepartmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("User", table =>
        {
            table.HasCheckConstraint("CK_User_AccountPlane", "(\"TenantId\" IS NULL AND \"IsPlatformAdministrator\" AND \"DepartmentId\" IS NULL) OR (\"TenantId\" IS NOT NULL AND NOT \"IsPlatformAdministrator\")");
            table.HasCheckConstraint("CK_User_AccessFailedCount", "\"AccessFailedCount\" >= 0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("UserId");
        b.Property(x => x.TenantId).IsConcurrencyToken();
        b.Property(x => x.EmployeeCode).HasMaxLength(50); b.Property(x => x.NormalizedEmployeeCode).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(320); b.Property(x => x.NormalizedEmail).HasMaxLength(320);
        b.Property(x => x.FullName).HasMaxLength(200); b.Property(x => x.SecurityStamp).HasMaxLength(64);
        b.HasIndex(x => new { x.TenantId, x.NormalizedEmployeeCode }).IsUnique().AreNullsDistinct(false);
        b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique().AreNullsDistinct(false);
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => new { x.TenantId, x.DepartmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Role", table => table.HasCheckConstraint("CK_Role_Scope", "(\"IsSystem\" AND \"TenantId\" IS NULL) OR (NOT \"IsSystem\" AND \"TenantId\" IS NOT NULL)"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("RoleId"); b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.TenantId).IsConcurrencyToken();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().AreNullsDistinct(false);
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permission"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("PermissionId");
        b.Property(x => x.Code).HasMaxLength(120); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Module).HasMaxLength(80); b.Property(x => x.Action).HasMaxLength(80);
    }

    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRole"); b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermission"); b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<AuthenticationSession> b)
    {
        b.ToTable("AuthenticationSession", table =>
        {
            table.HasCheckConstraint("CK_AuthenticationSession_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_AuthenticationSession_Hash", "\"TokenHash\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("CK_AuthenticationSession_Rotation", "\"ReplacedById\" IS NULL OR (\"RevokedAt\" IS NOT NULL AND \"ReplacedById\" <> \"AuthenticationSessionId\")");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("AuthenticationSessionId");
        b.Property(x => x.UserId).IsConcurrencyToken();
        b.Property(x => x.TokenHash).HasMaxLength(64); b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.FamilyId); b.HasIndex(x => x.ExpiresAt);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AuthenticationSession>().WithMany().HasForeignKey(x => x.ReplacedById).OnDelete(DeleteBehavior.Restrict);
        b.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLog"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("AuditLogId");
        b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.ObjectType).HasMaxLength(60);
        b.Property(x => x.BeforeJson).HasColumnType("jsonb"); b.Property(x => x.AfterJson).HasColumnType("jsonb");
        b.Property(x => x.MetadataJson).HasColumnType("jsonb");
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.CreatedAt, x.Id });
    }
}
