using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProposalAntiCheat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Notifications"
                WHERE "ReferenceType" = 'FreelancerCheatingViolation';

                UPDATE "Users"
                SET "SuspendedAt" = NULL,
                    "SuspendedUntil" = NULL,
                    "SuspensionReason" = NULL,
                    "UpdatedAt" = now()
                WHERE "SuspensionReason" = 'Suspended for repeated cheating during interview questions.';
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating");

            migrationBuilder.DropTable(
                name: "FreelancerCheatingViolations");

            migrationBuilder.DropTable(
                name: "ProposalCheatingEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This rollback restores schema only. Removed event, violation,
            // notification, and suspension data must be restored from the
            // production archive described in the deployment runbook.
            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty");

            migrationBuilder.CreateTable(
                name: "FreelancerCheatingViolations",
                columns: table => new
                {
                    FreelancerCheatingViolationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CopyCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EloDelta = table.Column<int>(type: "integer", nullable: false),
                    FocusLossCount = table.Column<int>(type: "integer", nullable: false),
                    FullscreenExitCount = table.Column<int>(type: "integer", nullable: false),
                    IsReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    PasteCount = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScreenshotAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    SuspendedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TabSwitchCount = table.Column<int>(type: "integer", nullable: false),
                    TotalEventCount = table.Column<int>(type: "integer", nullable: false),
                    ViolationNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("FreelancerCheatingViolations_pkey", x => x.FreelancerCheatingViolationsId);
                    table.ForeignKey(
                        name: "FreelancerCheatingViolations_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FreelancerCheatingViolations_usr_FreelancerUserId_fkey",
                        column: x => x.FreelancerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FreelancerCheatingViolations_usr_ReviewedByAdminId_fkey",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProposalCheatingEvents",
                columns: table => new
                {
                    ProposalCheatingEventsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostQuestionsId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientEventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalCheatingEvents_pkey", x => x.ProposalCheatingEventsId);
                    table.ForeignKey(
                        name: "ProposalCheatingEvents_jpq_JobPostQuestionsId_fkey",
                        column: x => x.JobPostQuestionsId,
                        principalTable: "JobPostQuestions",
                        principalColumn: "JobPostQuestionsId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "ProposalCheatingEvents_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ProposalCheatingEvents_usr_FreelancerUserId_fkey",
                        column: x => x.FreelancerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerCheatingViolations_FreelancerUserId_CreatedAt",
                table: "FreelancerCheatingViolations",
                columns: new[] { "FreelancerUserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerCheatingViolations_IsReviewed",
                table: "FreelancerCheatingViolations",
                column: "IsReviewed");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerCheatingViolations_ProposalsId",
                table: "FreelancerCheatingViolations",
                column: "ProposalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerCheatingViolations_ReviewedByAdminId",
                table: "FreelancerCheatingViolations",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCheatingEvents_EventType",
                table: "ProposalCheatingEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCheatingEvents_FreelancerUserId_CreatedAt",
                table: "ProposalCheatingEvents",
                columns: new[] { "FreelancerUserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCheatingEvents_JobPostQuestionsId",
                table: "ProposalCheatingEvents",
                column: "JobPostQuestionsId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCheatingEvents_ProposalsId_ClientEventId",
                table: "ProposalCheatingEvents",
                columns: new[] { "ProposalsId", "ClientEventId" },
                unique: true);
        }
    }
}
