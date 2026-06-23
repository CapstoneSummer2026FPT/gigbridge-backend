using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobInvitations",
                columns: table => new
                {
                    JobInvitationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum JobInvitationStatus: 0=Pending, 1=Viewed, 2=Applied, 3=Declined, 4=Expired, 5=Cancelled"),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("JobInvitations_pkey", x => x.JobInvitationsId);
                    table.ForeignKey(
                        name: "JobInvitations_clPro_ClientProfilesId_fkey",
                        column: x => x.ClientProfilesId,
                        principalTable: "ClientProfiles",
                        principalColumn: "ClientProfilesId");
                    table.ForeignKey(
                        name: "JobInvitations_flPro_FreelancerProfilesId_fkey",
                        column: x => x.FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId");
                    table.ForeignKey(
                        name: "JobInvitations_jp_JobPostsId_fkey",
                        column: x => x.JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId");
                    table.ForeignKey(
                        name: "JobInvitations_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_ClientProfilesId",
                table: "JobInvitations",
                column: "ClientProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_ClientProfilesId_JobPostsId",
                table: "JobInvitations",
                columns: new[] { "ClientProfilesId", "JobPostsId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_FreelancerProfilesId",
                table: "JobInvitations",
                column: "FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_FreelancerProfilesId_Status",
                table: "JobInvitations",
                columns: new[] { "FreelancerProfilesId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_JobPostsId",
                table: "JobInvitations",
                column: "JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_ProposalsId",
                table: "JobInvitations",
                column: "ProposalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobInvitations_Status",
                table: "JobInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "JobInvitations_jp_JobPostsId_flPro_FreelancerProfilesId_key",
                table: "JobInvitations",
                columns: new[] { "JobPostsId", "FreelancerProfilesId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobInvitations");
        }
    }
}
