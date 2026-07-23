using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteContractBusinessFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"AiInterviewDefinitions\" DROP COLUMN IF EXISTS \"ClickCount\";");

            migrationBuilder.AddColumn<Guid>(
                name: "ProposalMilestonePlansId",
                table: "ProposalWorkBreakdownItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractsId",
                table: "NegotiationOffers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql("ALTER TABLE \"JobPostPromotions\" ADD COLUMN IF NOT EXISTS \"ClickCount\" integer NOT NULL DEFAULT 0;");

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ContractWorkItems",
                columns: table => new
                {
                    ContractWorkItemId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MilestonesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProgressNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractWorkItems", x => x.ContractWorkItemId);
                    table.ForeignKey(
                        name: "FK_ContractWorkItems_Milestones_MilestonesId",
                        column: x => x.MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobPostMilestonePlans",
                columns: table => new
                {
                    JobPostMilestonePlanId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostMilestonePlans", x => x.JobPostMilestonePlanId);
                    table.ForeignKey(
                        name: "FK_JobPostMilestonePlans_JobPosts_JobPostsId",
                        column: x => x.JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NegotiationMilestoneDraftWorkItems",
                columns: table => new
                {
                    NegotiationMilestoneDraftWorkItemId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NegotiationMilestoneDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NegotiationMilestoneDraftWorkItems", x => x.NegotiationMilestoneDraftWorkItemId);
                    table.ForeignKey(
                        name: "FK_NegotiationMilestoneDraftWorkItems_NegotiationMilestoneDraf~",
                        column: x => x.NegotiationMilestoneDraftId,
                        principalTable: "NegotiationMilestoneDrafts",
                        principalColumn: "NegotiationMilestoneDraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NegotiationOfferWorkItems",
                columns: table => new
                {
                    NegotiationOfferWorkItemId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NegotiationOfferMilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NegotiationOfferWorkItems", x => x.NegotiationOfferWorkItemId);
                    table.ForeignKey(
                        name: "FK_NegotiationOfferWorkItems_NegotiationOfferMilestones_Negoti~",
                        column: x => x.NegotiationOfferMilestoneId,
                        principalTable: "NegotiationOfferMilestones",
                        principalColumn: "NegotiationOfferMilestoneId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobPostWorkItems",
                columns: table => new
                {
                    JobPostWorkItemId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    JobPostMilestonePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostWorkItems", x => x.JobPostWorkItemId);
                    table.ForeignKey(
                        name: "FK_JobPostWorkItems_JobPostMilestonePlans_JobPostMilestonePlan~",
                        column: x => x.JobPostMilestonePlanId,
                        principalTable: "JobPostMilestonePlans",
                        principalColumn: "JobPostMilestonePlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalWorkBreakdownItems_ProposalMilestonePlansId",
                table: "ProposalWorkBreakdownItems",
                column: "ProposalMilestonePlansId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorkItems_MilestonesId_OrderIndex",
                table: "ContractWorkItems",
                columns: new[] { "MilestonesId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostMilestonePlans_JobPostsId_OrderIndex",
                table: "JobPostMilestonePlans",
                columns: new[] { "JobPostsId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostWorkItems_JobPostMilestonePlanId_OrderIndex",
                table: "JobPostWorkItems",
                columns: new[] { "JobPostMilestonePlanId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationMilestoneDraftWorkItems_NegotiationMilestoneDraf~",
                table: "NegotiationMilestoneDraftWorkItems",
                columns: new[] { "NegotiationMilestoneDraftId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOfferWorkItems_NegotiationOfferMilestoneId_Order~",
                table: "NegotiationOfferWorkItems",
                columns: new[] { "NegotiationOfferMilestoneId", "OrderIndex" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProposalWorkBreakdownItems_ProposalMilestonePlans_ProposalM~",
                table: "ProposalWorkBreakdownItems",
                column: "ProposalMilestonePlansId",
                principalTable: "ProposalMilestonePlans",
                principalColumn: "ProposalMilestonePlansId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProposalWorkBreakdownItems_ProposalMilestonePlans_ProposalM~",
                table: "ProposalWorkBreakdownItems");

            migrationBuilder.DropTable(
                name: "ContractWorkItems");

            migrationBuilder.DropTable(
                name: "JobPostWorkItems");

            migrationBuilder.DropTable(
                name: "NegotiationMilestoneDraftWorkItems");

            migrationBuilder.DropTable(
                name: "NegotiationOfferWorkItems");

            migrationBuilder.DropTable(
                name: "JobPostMilestonePlans");

            migrationBuilder.DropIndex(
                name: "IX_ProposalWorkBreakdownItems_ProposalMilestonePlansId",
                table: "ProposalWorkBreakdownItems");

            migrationBuilder.DropColumn(
                name: "ProposalMilestonePlansId",
                table: "ProposalWorkBreakdownItems");

            migrationBuilder.Sql("ALTER TABLE \"JobPostPromotions\" DROP COLUMN IF EXISTS \"ClickCount\";");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "Contracts");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractsId",
                table: "NegotiationOffers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql("ALTER TABLE \"AiInterviewDefinitions\" ADD COLUMN IF NOT EXISTS \"ClickCount\" integer NOT NULL DEFAULT 0;");
        }
    }
}
