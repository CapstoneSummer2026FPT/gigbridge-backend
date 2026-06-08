using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEloPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEloPointTransactions",
                columns: table => new
                {
                    UserEloPointTransactionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsDelta = table.Column<int>(type: "integer", nullable: false),
                    PointsBefore = table.Column<int>(type: "integer", nullable: false),
                    PointsAfter = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Reason = table.Column<int>(type: "integer", nullable: false, comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating"),
                    SourceEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserEloPointTransactions_pkey", x => x.UserEloPointTransactionsId);
                    table.CheckConstraint("CK_UserEloPointTransactions_PointsAfter_NonNegative", "\"PointsAfter\" >= 0");
                    table.ForeignKey(
                        name: "UserEloPointTransactions_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserEloScores",
                columns: table => new
                {
                    UserEloScoresId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentPoints = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastInactivityPenaltyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReturnBonusAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserEloScores_pkey", x => x.UserEloScoresId);
                    table.CheckConstraint("CK_UserEloScores_CurrentPoints_NonNegative", "\"CurrentPoints\" >= 0");
                    table.ForeignKey(
                        name: "UserEloScores_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_IdempotencyKey",
                table: "UserEloPointTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_SourceEntity",
                table: "UserEloPointTransactions",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_UserId_CreatedAt",
                table: "UserEloPointTransactions",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserEloScores_CurrentPoints",
                table: "UserEloScores",
                column: "CurrentPoints",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_UserEloScores_UserId",
                table: "UserEloScores",
                column: "UserId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "UserEloScores" (
                    "UserEloScoresId",
                    "UserId",
                    "CurrentPoints",
                    "LastActivityAt",
                    "CreatedAt"
                )
                SELECT
                    gen_random_uuid(),
                    u."UserId",
                    100,
                    COALESCE(u."UpdatedAt", u."CreatedAt", now()),
                    now()
                FROM "Users" u
                WHERE u."Role" IN (0, 1)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "UserEloScores" s
                      WHERE s."UserId" = u."UserId"
                  );

                INSERT INTO "UserEloPointTransactions" (
                    "UserEloPointTransactionsId",
                    "UserId",
                    "PointsDelta",
                    "PointsBefore",
                    "PointsAfter",
                    "Reason",
                    "SourceEntityType",
                    "SourceEntityId",
                    "IdempotencyKey",
                    "Metadata",
                    "CreatedAt"
                )
                SELECT
                    gen_random_uuid(),
                    u."UserId",
                    100,
                    0,
                    100,
                    0,
                    'User',
                    u."UserId",
                    'initial:' || u."UserId"::text,
                    '{"source":"migration_backfill"}'::jsonb,
                    now()
                FROM "Users" u
                WHERE u."Role" IN (0, 1)
                ON CONFLICT ("IdempotencyKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEloPointTransactions");

            migrationBuilder.DropTable(
                name: "UserEloScores");
        }
    }
}
