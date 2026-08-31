using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "text", nullable: false),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousRefreshTokenHash = table.Column<string>(type: "text", nullable: true),
                    PreviousRefreshTokenGraceExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AuthSessions_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "AuthSessions_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_PreviousRefreshTokenHash",
                table: "AuthSessions",
                column: "PreviousRefreshTokenHash",
                filter: "\"PreviousRefreshTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_RefreshTokenHash",
                table: "AuthSessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId_LastUsedAt",
                table: "AuthSessions",
                columns: new[] { "UserId", "LastUsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId_RefreshTokenExpiry",
                table: "AuthSessions",
                columns: new[] { "UserId", "RefreshTokenExpiry" });

            // Preserve every currently logged-in browser. The existing hash remains valid
            // because only the plaintext cookie is needed to prove ownership during refresh.
            migrationBuilder.Sql(
                """
                INSERT INTO "AuthSessions" (
                    "Id",
                    "UserId",
                    "RefreshTokenHash",
                    "RefreshTokenExpiry",
                    "PreviousRefreshTokenHash",
                    "PreviousRefreshTokenGraceExpiresAt",
                    "CreatedAt",
                    "LastUsedAt")
                SELECT
                    gen_random_uuid(),
                    "UserId",
                    "RefreshTokenHash",
                    "RefreshTokenExpiry",
                    "PreviousRefreshTokenHash",
                    "PreviousRefreshTokenGraceExpiresAt",
                    COALESCE("UpdatedAt", "CreatedAt", now()),
                    COALESCE("UpdatedAt", "CreatedAt", now())
                FROM "Users"
                WHERE "RefreshTokenHash" IS NOT NULL
                  AND "RefreshTokenExpiry" IS NOT NULL
                ON CONFLICT ("RefreshTokenHash") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthSessions");
        }
    }
}
