using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractPlanChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractPlanChangeRequests",
                columns: table => new
                {
                    ContractPlanChangeRequestId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AffectedMilestoneIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    AffectedWorkItemIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractPlanChangeRequests", x => x.ContractPlanChangeRequestId);
                    table.ForeignKey(
                        name: "FK_ContractPlanChangeRequests_Contracts_ContractsId",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractPlanChangeRequests_ContractsId_CreatedAt",
                table: "ContractPlanChangeRequests",
                columns: new[] { "ContractsId", "CreatedAt" },
                filter: "\"ResolvedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractPlanChangeRequests");
        }
    }
}
