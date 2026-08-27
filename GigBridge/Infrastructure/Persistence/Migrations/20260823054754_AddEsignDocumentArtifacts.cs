using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEsignDocumentArtifacts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.CreateTable(
            name: "ESignDocumentArtifacts",
            columns: table => new
            {
                EsignDocumentArtifactId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                EsignDocumentsId = table.Column<Guid>(type: "uuid", nullable: false),
                ArtifactType = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<byte[]>(type: "bytea", nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                ContentHashSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                ArtifactRevision = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("ESignDocumentArtifacts_pkey", x => x.EsignDocumentArtifactId);
                table.ForeignKey(
                    name: "ESignDocumentArtifacts_eDoc_ESignDocumentsId_fkey",
                    column: x => x.EsignDocumentsId,
                    principalTable: "ESignDocuments",
                    principalColumn: "ESignDocumentsId",
                    onDelete: ReferentialAction.Cascade);
                table.CheckConstraint(
                    name: "CK_ESignDocumentArtifacts_ArtifactType",
                    sql: "\"ArtifactType\" IN (1, 2)");
                table.CheckConstraint(
                    name: "CK_ESignDocumentArtifacts_SizeBytes",
                    sql: "\"SizeBytes\" = octet_length(\"Content\") AND \"SizeBytes\" > 0");
            });

        migrationBuilder.CreateIndex(
            name: "ESignDocumentArtifacts_eDoc_Type_key",
            table: "ESignDocumentArtifacts",
            columns: new[] { "EsignDocumentsId", "ArtifactType" },
            unique: true);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ESignDocumentArtifacts");
    }
}
