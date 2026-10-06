using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskAssignment",
                columns: table => new
                {
                    TaskAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignment", x => x.TaskAssignmentId);
                    table.CheckConstraint("CK_TaskAssignment_Reason", "(\"RejectedAt\" IS NULL AND \"RejectionReason\" IS NULL) OR (\"RejectedAt\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL AND length(btrim(\"RejectionReason\")) > 0)");
                    table.CheckConstraint("CK_TaskAssignment_Receipt", "NOT (\"AcceptedAt\" IS NOT NULL AND \"RejectedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_TaskAssignment_Target", "\"DepartmentId\" IS NOT NULL OR \"UserId\" IS NOT NULL");
                    table.CheckConstraint("CK_TaskAssignment_Times", "(\"AcceptedAt\" IS NULL OR \"AcceptedAt\" >= \"AssignedAt\") AND (\"RejectedAt\" IS NULL OR \"RejectedAt\" >= \"AssignedAt\") AND (\"EndedAt\" IS NULL OR (\"EndedAt\" >= \"AssignedAt\" AND (\"AcceptedAt\" IS NULL OR \"AcceptedAt\" <= \"EndedAt\") AND (\"RejectedAt\" IS NULL OR \"RejectedAt\" <= \"EndedAt\")))");
                    table.ForeignKey(
                        name: "FK_TaskAssignment_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignment_Task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignment_User_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignment_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignment_AssignedBy",
                table: "TaskAssignment",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignment_DepartmentId",
                table: "TaskAssignment",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignment_TaskId",
                table: "TaskAssignment",
                column: "TaskId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignment_TaskId_AssignedAt",
                table: "TaskAssignment",
                columns: new[] { "TaskId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignment_UserId",
                table: "TaskAssignment",
                column: "UserId");

            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_task_assignment_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz; department_claim boolean := false;
                BEGIN
                    IF TG_OP IN ('DELETE','TRUNCATE') THEN
                        RAISE EXCEPTION 'Task assignment history cannot be deleted' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' THEN
                        department_claim := OLD."UserId" IS NULL AND NEW."UserId" IS NOT NULL AND OLD."DepartmentId" IS NOT NULL AND
                            OLD."AcceptedAt" IS NULL AND OLD."RejectedAt" IS NULL AND OLD."EndedAt" IS NULL AND
                            NEW."AcceptedAt" IS NOT NULL AND NEW."RejectedAt" IS NULL AND NEW."EndedAt" IS NULL;
                        IF OLD."EndedAt" IS NOT NULL THEN
                            RAISE EXCEPTION 'Ended assignments are immutable' USING ERRCODE='23514';
                        END IF;
                        IF NEW."TaskAssignmentId" IS DISTINCT FROM OLD."TaskAssignmentId" OR NEW."TaskId" IS DISTINCT FROM OLD."TaskId" OR
                           NEW."DepartmentId" IS DISTINCT FROM OLD."DepartmentId" OR (NEW."UserId" IS DISTINCT FROM OLD."UserId" AND NOT department_claim) OR
                           NEW."AssignedBy" IS DISTINCT FROM OLD."AssignedBy" OR NEW."AssignedAt" IS DISTINCT FROM OLD."AssignedAt" THEN
                            RAISE EXCEPTION 'Original assignment metadata is immutable' USING ERRCODE='23514';
                        END IF;
                        IF (OLD."AcceptedAt" IS NOT NULL AND NEW."AcceptedAt" IS DISTINCT FROM OLD."AcceptedAt") OR
                           (OLD."RejectedAt" IS NOT NULL AND (NEW."RejectedAt" IS DISTINCT FROM OLD."RejectedAt" OR NEW."RejectionReason" IS DISTINCT FROM OLD."RejectionReason")) THEN
                            RAISE EXCEPTION 'Assignment receipt decisions cannot be overwritten' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
                    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
                    IF NOT FOUND OR parent_deleted IS NOT NULL THEN
                        RAISE EXCEPTION 'Assignment requires a live task' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='INSERT' THEN
                        IF parent_status IN ('COMPLETED','CANCELLED') OR NEW."AcceptedAt" IS NOT NULL OR NEW."RejectedAt" IS NOT NULL OR
                           NEW."RejectionReason" IS NOT NULL OR NEW."EndedAt" IS NOT NULL THEN
                            RAISE EXCEPTION 'New assignment must be undecided on a nonterminal task' USING ERRCODE='23514';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."AssignedBy" AND "TenantId"=parent_tenant) THEN
                            RAISE EXCEPTION 'Assignment actor must belong to the task tenant' USING ERRCODE='23514';
                        END IF;
                        IF NEW."UserId" IS NOT NULL THEN
                            PERFORM 1 FROM "User" WHERE "UserId"=NEW."UserId" AND "TenantId"=parent_tenant AND "Status"='ACTIVE' AND "DeletedAt" IS NULL FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Assignment user target must be active in the task tenant' USING ERRCODE='23514';
                            END IF;
                        END IF;
                        IF NEW."DepartmentId" IS NOT NULL THEN
                            PERFORM 1 FROM "Department" WHERE "DepartmentId"=NEW."DepartmentId" AND "TenantId"=parent_tenant AND "Status"='ACTIVE' FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Assignment department target must be active in the task tenant' USING ERRCODE='23514';
                            END IF;
                        END IF;
                    ELSE
                        IF ((OLD."AcceptedAt" IS NULL AND NEW."AcceptedAt" IS NOT NULL) OR (OLD."RejectedAt" IS NULL AND NEW."RejectedAt" IS NOT NULL)) AND parent_status <> 'ASSIGNED' THEN
                            RAISE EXCEPTION 'Assignment receipt requires assigned task state' USING ERRCODE='23514';
                        END IF;
                        IF OLD."AcceptedAt" IS NULL AND NEW."AcceptedAt" IS NOT NULL THEN
                            PERFORM 1 FROM "User" WHERE "UserId"=NEW."UserId" AND "TenantId"=parent_tenant AND "Status"='ACTIVE' AND "DeletedAt" IS NULL AND
                                (NOT department_claim OR "DepartmentId"=OLD."DepartmentId") FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Acceptance requires its active user target or a one-time department-member claim' USING ERRCODE='23514';
                            END IF;
                        END IF;
                    END IF;
                    IF NEW."RejectionReason" IS NOT NULL AND (NEW."RejectionReason" !~ '[^[:space:]]' OR translate(NEW."RejectionReason", E'\r\n\t', '') ~ '[[:cntrl:]]') THEN
                        RAISE EXCEPTION 'Rejection reason must be nonblank plain text' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER task_assignment_guard BEFORE INSERT OR UPDATE OR DELETE ON "TaskAssignment"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_task_assignment_guard();
                CREATE TRIGGER task_assignment_truncate_guard BEFORE TRUNCATE ON "TaskAssignment"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_task_assignment_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TaskAssignment") THEN
                        RAISE EXCEPTION 'Cannot discard assignment history during rollback' USING ERRCODE='23514';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "TaskAssignment");
            migrationBuilder.Sql("DROP FUNCTION bizflow_task_assignment_guard();");
        }
    }
}
