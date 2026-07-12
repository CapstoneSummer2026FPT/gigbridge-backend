using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumRankProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Notifications",
                type: "integer",
                nullable: false,
                comment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring");

            migrationBuilder.CreateTable(
                name: "FreelancerRankProtections",
                columns: table => new
                {
                    FreelancerRankProtectionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsVacationModeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RankProtectionStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RankProtectionEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RankProtectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("FreelancerRankProtections_pkey", x => x.FreelancerRankProtectionsId);
                    table.ForeignKey(
                        name: "FreelancerRankProtections_FreelancerProfileId_fkey",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerRankProtections_End",
                table: "FreelancerRankProtections",
                column: "RankProtectionEndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerRankProtections_Profile_Active",
                table: "FreelancerRankProtections",
                columns: new[] { "FreelancerProfileId", "IsVacationModeEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreelancerRankProtections");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Notifications",
                type: "integer",
                nullable: false,
                comment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired");
        }
    }
}
