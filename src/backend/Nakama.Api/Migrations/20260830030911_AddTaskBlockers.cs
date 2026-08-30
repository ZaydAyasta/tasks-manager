using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakama.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskBlockers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status_before_blocked",
                table: "tasks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_blockers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_blockers", x => x.id);
                    table.CheckConstraint("ck_task_blockers_type", "type IN ('Dependency', 'Information', 'Approval', 'TechnicalIssue', 'External', 'Other')");
                    table.ForeignKey(
                        name: "FK_task_blockers_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_blockers_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_blockers_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_status_before_blocked",
                table: "tasks",
                sql: "status_before_blocked IS NULL OR status_before_blocked IN ('Pending', 'InProgress', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "IX_task_blockers_reported_by_user_id",
                table: "task_blockers",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_blockers_resolved_at",
                table: "task_blockers",
                column: "resolved_at");

            migrationBuilder.CreateIndex(
                name: "IX_task_blockers_resolved_by_user_id",
                table: "task_blockers",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_blockers_task_id",
                table: "task_blockers",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_blockers_active_task_id",
                table: "task_blockers",
                column: "task_id",
                filter: "resolved_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_blockers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_status_before_blocked",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "status_before_blocked",
                table: "tasks");
        }
    }
}
