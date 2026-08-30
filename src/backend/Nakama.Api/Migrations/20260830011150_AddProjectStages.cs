using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakama.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_normalized = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stages", x => x.id);
                    table.CheckConstraint("ck_stages_position", "position >= 1");
                    table.ForeignKey(
                        name: "FK_stages_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_members", x => x.id);
                    table.CheckConstraint("ck_stage_members_role", "role IN ('Responsible', 'Member')");
                    table.ForeignKey(
                        name: "FK_stage_members_stages_stage_id",
                        column: x => x.stage_id,
                        principalTable: "stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stage_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stage_members_stage_id",
                table: "stage_members",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_stage_members_user_id",
                table: "stage_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_stage_members_stage_user",
                table: "stage_members",
                columns: new[] { "stage_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stages_project_id",
                table: "stages",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_stages_project_name",
                table: "stages",
                columns: new[] { "project_id", "name_normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stages_project_position",
                table: "stages",
                columns: new[] { "project_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stage_members");

            migrationBuilder.DropTable(
                name: "stages");
        }
    }
}
