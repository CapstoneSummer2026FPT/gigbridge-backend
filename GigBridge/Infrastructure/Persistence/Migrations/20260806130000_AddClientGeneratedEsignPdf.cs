using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260806130000_AddClientGeneratedEsignPdf")]
public partial class AddClientGeneratedEsignPdf : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>("PdfDocumentContent", "ESignDocuments", type: "bytea", nullable: true);
        migrationBuilder.AddColumn<string>("PdfDocumentFileName", "ESignDocuments", type: "character varying(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<int>("PdfSignatureCount", "ESignDocuments", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>("PdfDocumentHash", "ESignDocuments", type: "character varying(128)", maxLength: 128, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("PdfDocumentContent", "ESignDocuments");
        migrationBuilder.DropColumn("PdfDocumentFileName", "ESignDocuments");
        migrationBuilder.DropColumn("PdfSignatureCount", "ESignDocuments");
        migrationBuilder.DropColumn("PdfDocumentHash", "ESignDocuments");
    }
}
