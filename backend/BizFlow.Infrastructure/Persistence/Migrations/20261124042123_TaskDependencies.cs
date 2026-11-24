using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskDependency",
                columns: table => new
                {
                    TaskDependencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDependency", x => x.TaskDependencyId);
                    table.CheckConstraint("CK_TaskDependency_NotSelf", "\"TaskId\" <> \"DependsOnTaskId\"");
                    table.ForeignKey(
                        name: "FK_TaskDependency_Task_DependsOnTaskId",
                        column: x => x.DependsOnTaskId,
                        principalTable: "Task",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDependency_Task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDependency_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDependency_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependency_CreatedBy",
                table: "TaskDependency",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependency_DependsOnTaskId",
                table: "TaskDependency",
                column: "DependsOnTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependency_TaskId",
                table: "TaskDependency",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependency_TenantId_DependsOnTaskId_RemovedAt",
                table: "TaskDependency",
                columns: new[] { "TenantId", "DependsOnTaskId", "RemovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependency_TenantId_TaskId_DependsOnTaskId",
                table: "TaskDependency",
                columns: new[] { "TenantId", "TaskId", "DependsOnTaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskDependency");
        }
    }
}
