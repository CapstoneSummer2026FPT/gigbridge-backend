using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeExtensionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ClaimedAmount",
                table: "Disputes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Disputes",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedReportId",
                table: "Disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedResolution",
                table: "Disputes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RespondentId",
                table: "Disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Disputes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RelatedReportId",
                table: "Disputes",
                column: "RelatedReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RespondentId",
                table: "Disputes",
                column: "RespondentId");

            migrationBuilder.AddForeignKey(
                name: "Disputes_rc_RelatedReportId_fkey",
                table: "Disputes",
                column: "RelatedReportId",
                principalTable: "ReportContracts",
                principalColumn: "ReportContractId");

            migrationBuilder.AddForeignKey(
                name: "Disputes_usr_RespondentId_fkey",
                table: "Disputes",
                column: "RespondentId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Disputes_rc_RelatedReportId_fkey",
                table: "Disputes");

            migrationBuilder.DropForeignKey(
                name: "Disputes_usr_RespondentId_fkey",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_RelatedReportId",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_RespondentId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ClaimedAmount",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RelatedReportId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RequestedResolution",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RespondentId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Disputes");
        }
    }
}
