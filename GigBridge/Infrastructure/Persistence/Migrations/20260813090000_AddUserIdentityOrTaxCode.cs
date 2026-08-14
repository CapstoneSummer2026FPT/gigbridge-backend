using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260813090000_AddUserIdentityOrTaxCode")]
public partial class AddUserIdentityOrTaxCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IdentityOrTaxCode",
            table: "Users",
            type: "character varying(12)",
            maxLength: 12,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "Users" AS users
            SET "IdentityOrTaxCode" = latest."IdentityOrTaxCode"
            FROM (
                SELECT DISTINCT ON (signatures."UserId")
                    signatures."UserId",
                    regexp_replace(signatures."IdentityOrTaxCode", '\s', '', 'g') AS "IdentityOrTaxCode"
                FROM "ESignSignatures" AS signatures
                WHERE signatures."IdentityOrTaxCode" IS NOT NULL
                  AND regexp_replace(signatures."IdentityOrTaxCode", '\s', '', 'g') ~ '^(\d{9}|\d{12})$'
                ORDER BY signatures."UserId", COALESCE(signatures."SignedAt", signatures."DraftSubmittedAt", signatures."UpdatedAt", signatures."CreatedAt") DESC
            ) AS latest
            WHERE users."UserId" = latest."UserId"
              AND users."IdentityOrTaxCode" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IdentityOrTaxCode",
            table: "Users");
    }
}
