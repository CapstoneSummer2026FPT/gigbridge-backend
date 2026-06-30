using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalInterviewReviewSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalInterviewReviewSessions",
                columns: table => new
                {
                    ProposalInterviewReviewSessionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReviewableQuestionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalInterviewReviewSessions_pkey", x => x.ProposalInterviewReviewSessionsId);
                    table.ForeignKey(
                        name: "ProposalInterviewReviewSessions_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ProposalInterviewReviewSessions_usr_FreelancerUserId_fkey",
                        column: x => x.FreelancerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalInterviewReviewSessions_FreelancerUserId_CreatedAt",
                table: "ProposalInterviewReviewSessions",
                columns: new[] { "FreelancerUserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalInterviewReviewSessions_ProposalsId",
                table: "ProposalInterviewReviewSessions",
                column: "ProposalsId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalInterviewReviewSessions");
        }
    }
}
