using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEsignDocxArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ESignDocuments"
                    DROP CONSTRAINT IF EXISTS "ESignDocuments_cont_ContractsId_key";
                DROP INDEX IF EXISTS "ESignDocuments_cont_ContractsId_key";
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "PolicyAcceptedAt",
                table: "ESignSignatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "ESignSignatures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractSnapshotJson",
                table: "ESignDocuments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FinalizedDocumentContent",
                table: "ESignDocuments",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalizedDocumentFileName",
                table: "ESignDocuments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalizedDocumentMimeType",
                table: "ESignDocuments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FinalizedDocumentSizeBytes",
                table: "ESignDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleId",
                table: "DeliveryOutboxes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_ESignDocuments_ContractsId",
                table: "ESignDocuments",
                column: "ContractsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ESignDocuments_ContractsId",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "PolicyAcceptedAt",
                table: "ESignSignatures");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "ESignSignatures");

            migrationBuilder.DropColumn(
                name: "ContractSnapshotJson",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedDocumentContent",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedDocumentFileName",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedDocumentMimeType",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedDocumentSizeBytes",
                table: "ESignDocuments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleId",
                table: "DeliveryOutboxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ESignDocuments_cont_ContractsId_key",
                table: "ESignDocuments",
                column: "ContractsId",
                unique: true);
        }
    }
}
