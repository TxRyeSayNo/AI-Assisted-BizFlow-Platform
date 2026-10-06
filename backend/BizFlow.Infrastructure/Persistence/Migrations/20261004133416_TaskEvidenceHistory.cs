using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskEvidenceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskProgressReport",
                columns: table => new
                {
                    ProgressReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percent = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Content = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskProgressReport", x => x.ProgressReportId);
                    table.CheckConstraint("CK_TaskProgressReport_Percent", "\"Percent\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_TaskProgressReport_Task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskProgressReport_User_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskResult",
                columns: table => new
                {
                    TaskResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    RevisionNo = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskResult", x => x.TaskResultId);
                    table.CheckConstraint("CK_TaskResult_Revision", "\"RevisionNo\" > 0");
                    table.ForeignKey(
                        name: "FK_TaskResult_Task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskResult_User_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressReport_AuthorId",
                table: "TaskProgressReport",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressReport_TaskId_SubmittedAt",
                table: "TaskProgressReport",
                columns: new[] { "TaskId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskResult_AuthorId",
                table: "TaskResult",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskResult_TaskId_RevisionNo",
                table: "TaskResult",
                columns: new[] { "TaskId", "RevisionNo" });

            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_task_evidence_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Task evidence is append-only' USING ERRCODE='23514';
                    END IF;
                    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
                    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
                    IF NOT FOUND OR parent_deleted IS NOT NULL THEN
                        RAISE EXCEPTION 'Evidence requires a live task' USING ERRCODE='23514';
                    END IF;
                    -- ADR-0003: overdue work must resume before result submission.
                    IF TG_TABLE_NAME='TaskResult' AND parent_status <> 'IN_PROGRESS' THEN
                        RAISE EXCEPTION 'Result submission requires in-progress work' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."AuthorId" AND "TenantId"=parent_tenant) THEN
                        RAISE EXCEPTION 'Evidence author must belong to the task tenant' USING ERRCODE='23514';
                    END IF;
                    IF translate(NEW."Content", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Evidence text contains unsupported control characters' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER task_progress_guard BEFORE INSERT OR UPDATE OR DELETE ON "TaskProgressReport"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_task_evidence_guard();
                CREATE TRIGGER task_progress_truncate_guard BEFORE TRUNCATE ON "TaskProgressReport"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_task_evidence_guard();
                CREATE TRIGGER task_result_guard BEFORE INSERT OR UPDATE OR DELETE ON "TaskResult"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_task_evidence_guard();
                CREATE TRIGGER task_result_truncate_guard BEFORE TRUNCATE ON "TaskResult"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_task_evidence_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TaskProgressReport") OR EXISTS (SELECT 1 FROM "TaskResult") THEN
                        RAISE EXCEPTION 'Cannot discard task evidence history during rollback' USING ERRCODE='23514';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "TaskProgressReport");

            migrationBuilder.DropTable(
                name: "TaskResult");
            migrationBuilder.Sql("DROP FUNCTION bizflow_task_evidence_guard();");
        }
    }
}
