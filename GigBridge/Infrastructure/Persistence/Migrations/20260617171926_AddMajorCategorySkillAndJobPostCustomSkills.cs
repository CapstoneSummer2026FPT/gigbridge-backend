using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMajorCategorySkillAndJobPostCustomSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var generalMajorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryCategoriesId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "Skills_cate_CategoriesId_fkey",
                table: "Skills");

            migrationBuilder.CreateTable(
                name: "Majors",
                columns: table => new
                {
                    MajorsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Majors_pkey", x => x.MajorsId);
                });

            migrationBuilder.InsertData(
                table: "Majors",
                columns: new[] { "MajorsId", "Name", "Slug", "Description", "IsActive", "SortOrder", "CreatedAt" },
                values: new object[]
                {
            generalMajorId,
            "General",
            "general",
            "Default major for existing categories",
            true,
            0,
            DateTime.UtcNow
                });

            migrationBuilder.AddColumn<string[]>(
                name: "CustomSkillNames",
                table: "JobPosts",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<Guid>(
                name: "MajorId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                defaultValue: generalMajorId);

            migrationBuilder.CreateTable(
                name: "CategorySkills",
                columns: table => new
                {
                    CategorySkillsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("CategorySkills_pkey", x => x.CategorySkillsId);

                    table.ForeignKey(
                        name: "CategorySkills_cat_CategoryId_fkey",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoriesId",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "CategorySkills_sk_SkillId_fkey",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "SkillsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
        INSERT INTO "CategorySkills" ("CategorySkillsId", "CategoryId", "SkillId", "CreatedAt")
        SELECT gen_random_uuid(), "CategoriesId", "SkillsId", now()
        FROM "Skills"
        WHERE "CategoriesId" IS NOT NULL;
    """);

            migrationBuilder.DropIndex(
                name: "IX_Skills_CategoriesId",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryCategoriesId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CategoriesId",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "ParentCategoryCategoriesId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_MajorId",
                table: "Categories",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "CategorySkills_CategoryId_SkillId_key",
                table: "CategorySkills",
                columns: new[] { "CategoryId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategorySkills_CategoryId",
                table: "CategorySkills",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorySkills_SkillId",
                table: "CategorySkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Majors_IsActive",
                table: "Majors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Majors_Slug",
                table: "Majors",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Majors_MajorId",
                table: "Categories",
                column: "MajorId",
                principalTable: "Majors",
                principalColumn: "MajorsId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Majors_MajorId",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "CategorySkills");

            migrationBuilder.DropTable(
                name: "Majors");

            migrationBuilder.DropIndex(
                name: "IX_Categories_MajorId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CustomSkillNames",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoriesId",
                table: "Skills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCategoryCategoriesId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CategoriesId",
                table: "Skills",
                column: "CategoriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryCategoriesId",
                table: "Categories",
                column: "ParentCategoryCategoriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryCategoriesId",
                table: "Categories",
                column: "ParentCategoryCategoriesId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");

            migrationBuilder.AddForeignKey(
                name: "Skills_cate_CategoriesId_fkey",
                table: "Skills",
                column: "CategoriesId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");
        }
    }
}
