using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakama.Api.Migrations
{
    /// <inheritdoc />
    public partial class RevokeLoggedOutSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revoked_sessions",
                columns: table => new
                {
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revoked_sessions", x => x.fingerprint);
                });

            migrationBuilder.CreateIndex(
                name: "IX_revoked_sessions_expires_at",
                table: "revoked_sessions",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revoked_sessions");
        }
    }
}
