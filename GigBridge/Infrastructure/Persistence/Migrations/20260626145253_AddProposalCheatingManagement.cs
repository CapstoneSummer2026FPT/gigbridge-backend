using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalCheatingManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedUntil",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FreelancerCheatingViolations",
                columns: table => new
                {
                    FreelancerCheatingViolationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViolationNumber = table.Column<int>(type: "integer", nullable: false),
                    TotalEventCount = table.Column<int>(type: "integer", nullable: false),
                    CopyCount = table.Column<int>(type: "integer", nullable: false),
                    PasteCount = table.Column<int>(type: "integer", nullable: false),
                    TabSwitchCount = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    EloDelta = table.Column<int>(type: "integer", nullable: false),
                    SuspendedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostQuestionsId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ClientEventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreelancerCheatingViolations");

            migrationBuilder.DropTable(
                name: "ProposalCheatingEvents");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Users");
        }
    }
}
