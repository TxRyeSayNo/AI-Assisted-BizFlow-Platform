using System;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .Annotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED");

            migrationBuilder.CreateTable(
                name: "Company",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<CompanyStatus>(type: "company_status", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Module = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ScopeType = table.Column<PermissionScope>(type: "permission_scope", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "Tenant",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<TenantStatus>(type: "tenant_status", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_Tenant_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    AuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorType = table.Column<AuditActorType>(type: "audit_actor_type", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_AuditLog_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Department",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<RecordStatus>(type: "record_status", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.DepartmentId);
                    table.UniqueConstraint("AK_Department_TenantId_DepartmentId", x => new { x.TenantId, x.DepartmentId });
                    table.CheckConstraint("CK_Department_NoSelfParent", "\"ParentDepartmentId\" IS NULL OR \"ParentDepartmentId\" <> \"DepartmentId\"");
                    table.ForeignKey(
                        name: "FK_Department_Department_TenantId_ParentDepartmentId",
                        columns: x => new { x.TenantId, x.ParentDepartmentId },
                        principalTable: "Department",
                        principalColumns: new[] { "TenantId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Department_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<RecordStatus>(type: "record_status", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.RoleId);
                    table.CheckConstraint("CK_Role_Scope", "(\"IsSystem\" AND \"TenantId\" IS NULL) OR (NOT \"IsSystem\" AND \"TenantId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Role_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPlatformAdministrator = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NormalizedEmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<UserStatus>(type: "user_status", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.UserId);
                    table.CheckConstraint("CK_User_AccessFailedCount", "\"AccessFailedCount\" >= 0");
                    table.CheckConstraint("CK_User_AccountPlane", "(\"TenantId\" IS NULL AND \"IsPlatformAdministrator\" AND \"DepartmentId\" IS NULL) OR (\"TenantId\" IS NOT NULL AND NOT \"IsPlatformAdministrator\")");
                    table.ForeignKey(
                        name: "FK_User_Department_TenantId_DepartmentId",
                        columns: x => new { x.TenantId, x.DepartmentId },
                        principalTable: "Department",
                        principalColumns: new[] { "TenantId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_User_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permission",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationSession",
                columns: table => new
                {
                    AuthenticationSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationSession", x => x.AuthenticationSessionId);
                    table.CheckConstraint("CK_AuthenticationSession_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_AuthenticationSession_Hash", "\"TokenHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_AuthenticationSession_Rotation", "\"ReplacedById\" IS NULL OR (\"RevokedAt\" IS NOT NULL AND \"ReplacedById\" <> \"AuthenticationSessionId\")");
                    table.ForeignKey(
                        name: "FK_AuthenticationSession_AuthenticationSession_ReplacedById",
                        column: x => x.ReplacedById,
                        principalTable: "AuthenticationSession",
                        principalColumn: "AuthenticationSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSession_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_CreatedAt_AuditLogId",
                table: "AuditLog",
                columns: new[] { "TenantId", "CreatedAt", "AuditLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSession_ExpiresAt",
                table: "AuthenticationSession",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSession_FamilyId",
                table: "AuthenticationSession",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSession_ReplacedById",
                table: "AuthenticationSession",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSession_TokenHash",
                table: "AuthenticationSession",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSession_UserId",
                table: "AuthenticationSession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Company_Code",
                table: "Company",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Department_TenantId_Code",
                table: "Department",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Department_TenantId_ParentDepartmentId",
                table: "Department",
                columns: new[] { "TenantId", "ParentDepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Permission_Code",
                table: "Permission",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Role_TenantId_Name",
                table: "Role",
                columns: new[] { "TenantId", "Name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_CompanyId",
                table: "Tenant",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_TenantKey",
                table: "Tenant",
                column: "TenantKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId_DepartmentId",
                table: "User",
                columns: new[] { "TenantId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId_NormalizedEmail",
                table: "User",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId_NormalizedEmployeeCode",
                table: "User",
                columns: new[] { "TenantId", "NormalizedEmployeeCode" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");

            // BR-015/020: append-only audit is enforced even outside EF's change tracker.
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Audit records are append-only' USING ERRCODE = '23514';
                END $$;
                CREATE TRIGGER audit_no_mutation BEFORE UPDATE OR DELETE ON "AuditLog"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_audit_immutable();
                CREATE TRIGGER audit_no_truncate BEFORE TRUNCATE ON "AuditLog"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_audit_immutable();

                CREATE FUNCTION bizflow_tenant_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" THEN
                        RAISE EXCEPTION 'Tenant ownership is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER user_tenant_immutable BEFORE UPDATE ON "User"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_tenant_immutable();
                CREATE TRIGGER department_tenant_immutable BEFORE UPDATE ON "Department"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_tenant_immutable();
                CREATE TRIGGER role_tenant_immutable BEFORE UPDATE ON "Role"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_tenant_immutable();

                CREATE FUNCTION bizflow_user_role_scope() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE user_tenant uuid; role_tenant uuid;
                BEGIN
                    SELECT "TenantId" INTO user_tenant FROM "User" WHERE "UserId" = NEW."UserId";
                    SELECT "TenantId" INTO role_tenant FROM "Role" WHERE "RoleId" = NEW."RoleId";
                    IF role_tenant IS NOT NULL AND role_tenant IS DISTINCT FROM user_tenant THEN
                        RAISE EXCEPTION 'Cross-tenant role assignment is forbidden' USING ERRCODE = '23514';
                    END IF;
                    IF user_tenant IS NOT NULL AND EXISTS (
                        SELECT 1 FROM "RolePermission" rp JOIN "Permission" p ON p."PermissionId" = rp."PermissionId"
                        WHERE rp."RoleId" = NEW."RoleId" AND p."ScopeType" = 'PLATFORM') THEN
                        RAISE EXCEPTION 'Platform permissions cannot be assigned to tenant accounts' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER user_role_scope BEFORE INSERT OR UPDATE ON "UserRole"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_user_role_scope();

                CREATE FUNCTION bizflow_role_permission_scope() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Permission" WHERE "PermissionId" = NEW."PermissionId" AND "ScopeType" = 'PLATFORM')
                        AND (EXISTS (SELECT 1 FROM "Role" WHERE "RoleId" = NEW."RoleId" AND NOT "IsSystem")
                            OR EXISTS (SELECT 1 FROM "UserRole" ur JOIN "User" u ON u."UserId" = ur."UserId"
                                WHERE ur."RoleId" = NEW."RoleId" AND u."TenantId" IS NOT NULL)) THEN
                        RAISE EXCEPTION 'Tenant roles cannot grant platform permissions' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER role_permission_scope BEFORE INSERT OR UPDATE ON "RolePermission"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_role_permission_scope();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION bizflow_audit_immutable() CASCADE;
                DROP FUNCTION bizflow_tenant_immutable() CASCADE;
                DROP FUNCTION bizflow_user_role_scope() CASCADE;
                DROP FUNCTION bizflow_role_permission_scope() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "AuthenticationSession");

            migrationBuilder.DropTable(
                name: "RolePermission");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Department");

            migrationBuilder.DropTable(
                name: "Tenant");

            migrationBuilder.DropTable(
                name: "Company");
        }
    }
}
