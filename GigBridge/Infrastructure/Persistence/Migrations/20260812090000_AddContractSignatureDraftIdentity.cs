using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260812090000_AddContractSignatureDraftIdentity")]
public partial class AddContractSignatureDraftIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DraftSubmittedAt",
            table: "ESignSignatures",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "IdentityOrTaxCode",
            table: "ESignSignatures",
            type: "character varying(12)",
            maxLength: 12,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "ESignSignatures",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "ESignSignatures" AS s
            SET "Status" = 0,
                "DraftSubmittedAt" = s."SignedAt",
                "UpdatedAt" = COALESCE(s."SignedAt", s."CreatedAt"),
                "SignedAt" = NULL
            FROM "ESignDocuments" AS d
            WHERE s."ESignDocumentsId" = d."ESignDocumentsId"
              AND d."ContractsId" IS NOT NULL
              AND d."Status" IN (1, 2)
              AND s."Status" = 1
              AND (
                  SELECT COUNT(*) FROM "ESignSignatures" AS signed
                  WHERE signed."ESignDocumentsId" = d."ESignDocumentsId"
                    AND signed."Status" = 1) = 1;

            UPDATE "ESignDocuments"
            SET "Status" = 1,
                "FinalizedAt" = NULL,
                "FinalizedDocumentContent" = NULL,
                "FinalizedDocumentFileName" = NULL,
                "FinalizedDocumentMimeType" = NULL,
                "FinalizedDocumentSizeBytes" = NULL,
                "ExportedPdfUrl" = NULL,
                "PdfDocumentContent" = NULL,
                "PdfDocumentFileName" = NULL,
                "PdfDocumentHash" = NULL,
                "PdfSignatureCount" = 0,
                "UpdatedAt" = NOW()
            WHERE "ContractsId" IS NOT NULL
              AND (
                  "Status" = 2
                  OR EXISTS (
                      SELECT 1 FROM "ESignSignatures" AS s
                      WHERE s."ESignDocumentsId" = "ESignDocuments"."ESignDocumentsId"
                        AND s."Status" = 0
                        AND s."DraftSubmittedAt" IS NOT NULL));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "ESignSignatures" AS s
            SET "Status" = 1,
                "SignedAt" = s."DraftSubmittedAt"
            FROM "ESignDocuments" AS d
            WHERE s."ESignDocumentsId" = d."ESignDocumentsId"
              AND d."ContractsId" IS NOT NULL
              AND d."Status" = 1
              AND s."Status" = 0
              AND s."DraftSubmittedAt" IS NOT NULL;

            UPDATE "ESignDocuments" AS d
            SET "Status" = 2
            WHERE d."ContractsId" IS NOT NULL
              AND d."Status" = 1
              AND EXISTS (
                  SELECT 1 FROM "ESignSignatures" AS s
                  WHERE s."ESignDocumentsId" = d."ESignDocumentsId"
                    AND s."Status" = 1);
            """);

        migrationBuilder.DropColumn(name: "DraftSubmittedAt", table: "ESignSignatures");
        migrationBuilder.DropColumn(name: "IdentityOrTaxCode", table: "ESignSignatures");
        migrationBuilder.DropColumn(name: "UpdatedAt", table: "ESignSignatures");
    }
}
