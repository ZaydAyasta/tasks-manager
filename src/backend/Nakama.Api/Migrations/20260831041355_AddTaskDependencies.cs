using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakama.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_dependencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depends_on_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_dependencies", x => x.id);
                    table.CheckConstraint("ck_task_dependencies_no_self", "task_id <> depends_on_task_id");
                    table.ForeignKey(
                        name: "FK_task_dependencies_tasks_depends_on_task_id",
                        column: x => x.depends_on_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_dependencies_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_dependencies_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_created_by_user_id",
                table: "task_dependencies",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_depends_on_task_id",
                table: "task_dependencies",
                column: "depends_on_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_task_id",
                table: "task_dependencies",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_task_id_depends_on_task_id",
                table: "task_dependencies",
                columns: new[] { "task_id", "depends_on_task_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_dependencies");
        }
    }
}
