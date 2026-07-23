using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteChangeRequestAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClarificationRequestNote",
                table: "ContractChangeRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClarificationResponseNote",
                table: "ContractChangeRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClarifiedAt",
                table: "ContractChangeRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseNote",
                table: "ContractChangeRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentSnapshotJson",
                table: "ContractAmendments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ContractAmendments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClarificationRequestNote",
                table: "ContractChangeRequests");

            migrationBuilder.DropColumn(
                name: "ClarificationResponseNote",
                table: "ContractChangeRequests");

            migrationBuilder.DropColumn(
                name: "ClarifiedAt",
                table: "ContractChangeRequests");

            migrationBuilder.DropColumn(
                name: "ResponseNote",
                table: "ContractChangeRequests");

            migrationBuilder.DropColumn(
                name: "DocumentSnapshotJson",
                table: "ContractAmendments");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ContractAmendments");
        }
    }
}
