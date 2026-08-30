using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakama.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subtasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subtasks", x => x.id);
                    table.CheckConstraint("ck_subtasks_position", "position >= 1");
                    table.ForeignKey(
                        name: "FK_subtasks_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subtasks_users_completed_by_user_id",
                        column: x => x.completed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subtasks_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subtasks_completed_by_user_id",
                table: "subtasks",
                column: "completed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_subtasks_created_by_user_id",
                table: "subtasks",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_subtasks_task_id",
                table: "subtasks",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_subtasks_task_id_position",
                table: "subtasks",
                columns: new[] { "task_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subtasks");
        }
    }
}
