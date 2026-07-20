using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteContractAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractChangeRequests",
                columns: table => new
                {
                    ContractChangeRequestId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespondedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestedChanges = table.Column<string>(type: "text", nullable: false),
                    AffectedMilestoneIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    AffectedWorkItemIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractChangeRequests", x => x.ContractChangeRequestId);
                    table.ForeignKey(
                        name: "FK_ContractChangeRequests_Contracts_ContractsId",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MilestoneEarlyStartRequests",
                columns: table => new
                {
                    MilestoneEarlyStartRequestId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestonesId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespondedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResponseNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneEarlyStartRequests", x => x.MilestoneEarlyStartRequestId);
                    table.ForeignKey(
                        name: "FK_MilestoneEarlyStartRequests_Contracts_ContractsId",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MilestoneEarlyStartRequests_Milestones_MilestonesId",
                        column: x => x.MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractAmendments",
                columns: table => new
                {
                    ContractAmendmentId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractChangeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OriginalTotalBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedTotalBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BudgetDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendments", x => x.ContractAmendmentId);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_ContractChangeRequests_ContractChangeReq~",
                        column: x => x.ContractChangeRequestId,
                        principalTable: "ContractChangeRequests",
                        principalColumn: "ContractChangeRequestId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Contracts_ContractsId",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractAmendmentMilestones",
                columns: table => new
                {
                    ContractAmendmentMilestoneId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractAmendmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendmentMilestones", x => x.ContractAmendmentMilestoneId);
                    table.ForeignKey(
                        name: "FK_ContractAmendmentMilestones_ContractAmendments_ContractAmen~",
                        column: x => x.ContractAmendmentId,
                        principalTable: "ContractAmendments",
                        principalColumn: "ContractAmendmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractAmendmentSignatures",
                columns: table => new
                {
                    ContractAmendmentSignatureId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractAmendmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignerRole = table.Column<int>(type: "integer", nullable: false),
                    SignatureData = table.Column<string>(type: "text", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendmentSignatures", x => x.ContractAmendmentSignatureId);
                    table.ForeignKey(
                        name: "FK_ContractAmendmentSignatures_ContractAmendments_ContractAmen~",
                        column: x => x.ContractAmendmentId,
                        principalTable: "ContractAmendments",
                        principalColumn: "ContractAmendmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractAmendmentWorkItems",
                columns: table => new
                {
                    ContractAmendmentWorkItemId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractAmendmentMilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceContractWorkItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendmentWorkItems", x => x.ContractAmendmentWorkItemId);
                    table.ForeignKey(
                        name: "FK_ContractAmendmentWorkItems_ContractAmendmentMilestones_Cont~",
                        column: x => x.ContractAmendmentMilestoneId,
                        principalTable: "ContractAmendmentMilestones",
                        principalColumn: "ContractAmendmentMilestoneId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendmentMilestones_ContractAmendmentId_OrderIndex",
                table: "ContractAmendmentMilestones",
                columns: new[] { "ContractAmendmentId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractChangeRequestId",
                table: "ContractAmendments",
                column: "ContractChangeRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractsId_RevisionNumber",
                table: "ContractAmendments",
                columns: new[] { "ContractsId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendmentSignatures_ContractAmendmentId_UserId",
                table: "ContractAmendmentSignatures",
                columns: new[] { "ContractAmendmentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendmentWorkItems_ContractAmendmentMilestoneId_Ord~",
                table: "ContractAmendmentWorkItems",
                columns: new[] { "ContractAmendmentMilestoneId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractChangeRequests_ContractsId",
                table: "ContractChangeRequests",
                column: "ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneEarlyStartRequests_ContractsId",
                table: "MilestoneEarlyStartRequests",
                column: "ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneEarlyStartRequests_MilestonesId_Status",
                table: "MilestoneEarlyStartRequests",
                columns: new[] { "MilestonesId", "Status" },
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractAmendmentSignatures");

            migrationBuilder.DropTable(
                name: "ContractAmendmentWorkItems");

            migrationBuilder.DropTable(
                name: "MilestoneEarlyStartRequests");

            migrationBuilder.DropTable(
                name: "ContractAmendmentMilestones");

            migrationBuilder.DropTable(
                name: "ContractAmendments");

            migrationBuilder.DropTable(
                name: "ContractChangeRequests");
        }
    }
}
