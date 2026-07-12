using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePhase2NegotiationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "Milestones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Deliverables",
                table: "Milestones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Milestones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedDuration",
                table: "Milestones",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NegotiationMilestoneDrafts",
                columns: table => new
                {
                    NegotiationMilestoneDraftId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConversationsId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProposalMilestonePlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("NegotiationMilestoneDrafts_pkey", x => x.NegotiationMilestoneDraftId);
                    table.ForeignKey(
                        name: "NegotiationMilestoneDrafts_ConversationsId_fkey",
                        column: x => x.ConversationsId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NegotiationOfferMilestones",
                columns: table => new
                {
                    NegotiationOfferMilestoneId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NegotiationOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("NegotiationOfferMilestones_pkey", x => x.NegotiationOfferMilestoneId);
                    table.ForeignKey(
                        name: "NegotiationOfferMilestones_NegotiationOfferId_fkey",
                        column: x => x.NegotiationOfferId,
                        principalTable: "NegotiationOffers",
                        principalColumn: "NegotiationOfferId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationMilestoneDrafts_Conversation_Order",
                table: "NegotiationMilestoneDrafts",
                columns: new[] { "ConversationsId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOfferMilestones_Offer_Order",
                table: "NegotiationOfferMilestones",
                columns: new[] { "NegotiationOfferId", "OrderIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NegotiationMilestoneDrafts");

            migrationBuilder.DropTable(
                name: "NegotiationOfferMilestones");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "Deliverables",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "Milestones");
        }
    }
}
