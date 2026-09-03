using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyEsignAndReceiptContentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Belt-and-suspenders: the upsert below is only airtight if nothing is concurrently
            // writing to these tables while it runs, which today depends on the deploy process
            // actually stopping old-code writers before this migration executes. Acquiring an
            // exclusive lock on all four tables up front makes that guarantee self-enforcing
            // instead of procedural — any transaction (old app instance included) that tries to
            // read or write these tables blocks until this migration's transaction commits,
            // instead of racing the upsert. The tables are small (low thousands of rows per the
            // egress remediation doc's own query-stats numbers), so this should hold the lock only
            // briefly.
            migrationBuilder.Sql(
                """
                LOCK TABLE "ESignDocuments", "ESignDocumentContents", "ProjectReceipts", "ProjectReceiptContents"
                    IN ACCESS EXCLUSIVE MODE;
                """);

            // Final resync, immediately before the point of no return: the previous migration's
            // backfill only captured a snapshot at the moment it ran. The still-running old app
            // version can keep INSERTing new rows *and* UPDATing existing ones (e.g. regenerating
            // a PDF, re-signing a document) in the legacy columns for as long as this deploy's
            // "verify migration 1" window lasts. A `WHERE NOT EXISTS` catch-up would only catch
            // brand-new rows and silently keep a stale (or entirely empty) content row for
            // anything that was merely *updated* during that window. Upsert every row
            // unconditionally instead, so whatever the legacy columns hold right now — the last
            // possible moment before they're dropped — is what ends up in the content tables.
            // This must run as a blocking pre-traffic migration step (not applied while old code
            // instances are still concurrently accepting writes), same as any drop-column migration
            // — the LOCK TABLE above is the safety net if that isn't guaranteed operationally.
            migrationBuilder.Sql(
                """
                INSERT INTO "ESignDocumentContents"
                    ("ESignDocumentsId", "RenderedHtmlContent", "ContractSnapshotJson",
                     "FinalizedDocumentContent", "FinalizedDocumentMimeType",
                     "PdfDocumentContent", "PdfDocumentFileName")
                SELECT d."ESignDocumentsId", d."RenderedHtmlContent", d."ContractSnapshotJson",
                       d."FinalizedDocumentContent", d."FinalizedDocumentMimeType",
                       d."PdfDocumentContent", d."PdfDocumentFileName"
                FROM "ESignDocuments" d
                ON CONFLICT ("ESignDocumentsId") DO UPDATE SET
                    "RenderedHtmlContent" = EXCLUDED."RenderedHtmlContent",
                    "ContractSnapshotJson" = EXCLUDED."ContractSnapshotJson",
                    "FinalizedDocumentContent" = EXCLUDED."FinalizedDocumentContent",
                    "FinalizedDocumentMimeType" = EXCLUDED."FinalizedDocumentMimeType",
                    "PdfDocumentContent" = EXCLUDED."PdfDocumentContent",
                    "PdfDocumentFileName" = EXCLUDED."PdfDocumentFileName";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ESignDocuments"
                SET "PdfDocumentSizeBytes" = octet_length("PdfDocumentContent");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ProjectReceiptContents"
                    ("ProjectReceiptId", "SnapshotJson", "SnapshotHashSha256",
                     "PdfContent", "PdfFileName", "PdfContentType", "PdfHashSha256")
                SELECT r."ProjectReceiptId", r."SnapshotJson", r."SnapshotHashSha256",
                       r."PdfContent", r."PdfFileName", r."PdfContentType", r."PdfHashSha256"
                FROM "ProjectReceipts" r
                ON CONFLICT ("ProjectReceiptId") DO UPDATE SET
                    "SnapshotJson" = EXCLUDED."SnapshotJson",
                    "SnapshotHashSha256" = EXCLUDED."SnapshotHashSha256",
                    "PdfContent" = EXCLUDED."PdfContent",
                    "PdfFileName" = EXCLUDED."PdfFileName",
                    "PdfContentType" = EXCLUDED."PdfContentType",
                    "PdfHashSha256" = EXCLUDED."PdfHashSha256";
                """);

            migrationBuilder.DropColumn(
                name: "PdfContent",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "PdfContentType",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "PdfFileName",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "PdfHashSha256",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "SnapshotHashSha256",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "SnapshotJson",
                table: "ProjectReceipts");

            migrationBuilder.DropColumn(
                name: "ContractSnapshotJson",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedDocumentContent",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedDocumentMimeType",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "PdfDocumentContent",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "PdfDocumentFileName",
                table: "ESignDocuments");

            migrationBuilder.DropColumn(
                name: "RenderedHtmlContent",
                table: "ESignDocuments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PdfContent",
                table: "ProjectReceipts",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfContentType",
                table: "ProjectReceipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFileName",
                table: "ProjectReceipts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfHashSha256",
                table: "ProjectReceipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHashSha256",
                table: "ProjectReceipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotJson",
                table: "ProjectReceipts",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}"); // "" is not valid JSON; Postgres rejects it as a jsonb default

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
                name: "FinalizedDocumentMimeType",
                table: "ESignDocuments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PdfDocumentContent",
                table: "ESignDocuments",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfDocumentFileName",
                table: "ESignDocuments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenderedHtmlContent",
                table: "ESignDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            // The AddColumn calls above only recreate empty columns. Restore the actual data from
            // the (still-present — migration A's Down() hasn't run yet) content tables, or rolling
            // back this migration would silently wipe every document's HTML/snapshot/PDF content.
            migrationBuilder.Sql(
                """
                UPDATE "ProjectReceipts" r
                SET "PdfContent" = c."PdfContent",
                    "PdfContentType" = c."PdfContentType",
                    "PdfFileName" = c."PdfFileName",
                    "PdfHashSha256" = c."PdfHashSha256",
                    "SnapshotHashSha256" = c."SnapshotHashSha256",
                    "SnapshotJson" = c."SnapshotJson"
                FROM "ProjectReceiptContents" c
                WHERE c."ProjectReceiptId" = r."ProjectReceiptId";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ESignDocuments" d
                SET "ContractSnapshotJson" = c."ContractSnapshotJson",
                    "FinalizedDocumentContent" = c."FinalizedDocumentContent",
                    "FinalizedDocumentMimeType" = c."FinalizedDocumentMimeType",
                    "PdfDocumentContent" = c."PdfDocumentContent",
                    "PdfDocumentFileName" = c."PdfDocumentFileName",
                    "RenderedHtmlContent" = c."RenderedHtmlContent"
                FROM "ESignDocumentContents" c
                WHERE c."ESignDocumentsId" = d."ESignDocumentsId";
                """);
        }
    }
}
