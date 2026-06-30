using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    public partial class AddContractProductHandoffs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractProductHandoffs",
                columns: table => new
                {
                    ContractProductHandoffId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false, comment: "Enum ContractProductHandoffSourceType: 0=File, 1=Link"),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ContractProductHandoffs_pkey", x => x.ContractProductHandoffId);
                    table.ForeignKey(
                        name: "ContractProductHandoffs_cont_ContractsId_fkey",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId");
                    table.ForeignKey(
                        name: "ContractProductHandoffs_usr_ReceivedByUserId_fkey",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "ContractProductHandoffs_usr_SubmittedByUserId_fkey",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractProductHandoffs_ContractsId",
                table: "ContractProductHandoffs",
                column: "ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractProductHandoffs_ContractsId_IsCurrent",
                table: "ContractProductHandoffs",
                columns: new[] { "ContractsId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractProductHandoffs_ContractsId_Version",
                table: "ContractProductHandoffs",
                columns: new[] { "ContractsId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractProductHandoffs_ReceivedByUserId",
                table: "ContractProductHandoffs",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractProductHandoffs_SubmittedByUserId",
                table: "ContractProductHandoffs",
                column: "SubmittedByUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractProductHandoffs");
        }
    }
}
