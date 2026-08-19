using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitEsignAndReceiptContentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentRevision",
                table: "ProjectReceipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContentRevision",
                table: "ESignDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "PdfDocumentSizeBytes",
                table: "ESignDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ESignDocumentContents",
                columns: table => new
                {
                    ESignDocumentsId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenderedHtmlContent = table.Column<string>(type: "text", nullable: false),
                    ContractSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    FinalizedDocumentContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    FinalizedDocumentMimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PdfDocumentContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    PdfDocumentFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ESignDocumentContents_pkey", x => x.ESignDocumentsId);
                    table.ForeignKey(
                        name: "ESignDocumentContents_eDoc_ESignDocumentsId_fkey",
                        column: x => x.ESignDocumentsId,
                        principalTable: "ESignDocuments",
                        principalColumn: "ESignDocumentsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReceiptContents",
                columns: table => new
                {
                    ProjectReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    PdfFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PdfContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PdfHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReceiptContents", x => x.ProjectReceiptId);
                    table.ForeignKey(
                        name: "FK_ProjectReceiptContents_ProjectReceipts_ProjectReceiptId",
                        column: x => x.ProjectReceiptId,
                        principalTable: "ProjectReceipts",
                        principalColumn: "ProjectReceiptId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill: copy the heavy columns into the new side tables, then compute the
            // lightweight size column the hot ESignDocuments table now relies on for presence
            // checks. Both tables are small enough in production (low thousands of rows at most,
            // per the query-stats numbers in the egress remediation doc) for a single
            // INSERT ... SELECT to be safe; re-chunk with a batched PL/pgSQL loop if row counts
            // grow enough that this starts holding a long lock.
            migrationBuilder.Sql(
                """
                INSERT INTO "ESignDocumentContents"
                    ("ESignDocumentsId", "RenderedHtmlContent", "ContractSnapshotJson",
                     "FinalizedDocumentContent", "FinalizedDocumentMimeType",
                     "PdfDocumentContent", "PdfDocumentFileName")
                SELECT "ESignDocumentsId", "RenderedHtmlContent", "ContractSnapshotJson",
                       "FinalizedDocumentContent", "FinalizedDocumentMimeType",
                       "PdfDocumentContent", "PdfDocumentFileName"
                FROM "ESignDocuments";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ESignDocuments"
                SET "PdfDocumentSizeBytes" = octet_length("PdfDocumentContent")
                WHERE "PdfDocumentContent" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ProjectReceiptContents"
                    ("ProjectReceiptId", "SnapshotJson", "SnapshotHashSha256",
                     "PdfContent", "PdfFileName", "PdfContentType", "PdfHashSha256")
                SELECT "ProjectReceiptId", "SnapshotJson", "SnapshotHashSha256",
                       "PdfContent", "PdfFileName", "PdfContentType", "PdfHashSha256"
                FROM "ProjectReceipts";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ESignDocumentContents");

            migrationBuilder.DropTable(
                name: "ProjectReceiptContents");

            migrationBuilder.DropColumn(
                name: "ContentRevision",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "ContentRevision",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "PdfDocumentSizeBytes",
                table: "ESignDocuments");
        }
    }
}
