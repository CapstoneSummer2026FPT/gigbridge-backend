using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixJobPostMajorCategoryRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Majors_MajorId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "JobPosts_CategoryId_fkey",
                table: "JobPosts");

            migrationBuilder.CreateTable(
                name: "MajorCategories",
                columns: table => new
                {
                    MajorCategoriesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MajorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MajorCategories_pkey", x => x.MajorCategoriesId);
                    table.ForeignKey(
                        name: "MajorCategories_cat_CategoryId_fkey",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoriesId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "MajorCategories_major_MajorId_fkey",
                        column: x => x.MajorId,
                        principalTable: "Majors",
                        principalColumn: "MajorsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MajorCategories_CategoryId",
                table: "MajorCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorCategories_MajorId",
                table: "MajorCategories",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "MajorCategories_MajorId_CategoryId_key",
                table: "MajorCategories",
                columns: new[] { "MajorId", "CategoryId" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "MajorCategories" ("MajorCategoriesId", "MajorId", "CategoryId", "CreatedAt")
                SELECT gen_random_uuid(), "MajorId", "CategoriesId", now()
                FROM "Categories"
                ON CONFLICT ("MajorId", "CategoryId") DO NOTHING;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "MajorCategoryId",
                table: "JobPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "JobPosts" AS job
                SET "MajorCategoryId" = major_category."MajorCategoriesId"
                FROM "MajorCategories" AS major_category
                WHERE job."CategoryId" = major_category."CategoryId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_MajorCategoryId",
                table: "JobPosts",
                column: "MajorCategoryId");

            migrationBuilder.AddForeignKey(
                name: "JobPosts_MajorCategoryId_fkey",
                table: "JobPosts",
                column: "MajorCategoryId",
                principalTable: "MajorCategories",
                principalColumn: "MajorCategoriesId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropIndex(
                name: "IX_JobPosts_CategoryId",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "JobPosts");

            migrationBuilder.DropIndex(
                name: "IX_Categories_MajorId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "JobPosts_MajorCategoryId_fkey",
                table: "JobPosts");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "JobPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "JobPosts" AS job
                SET "CategoryId" = major_category."CategoryId"
                FROM "MajorCategories" AS major_category
                WHERE job."MajorCategoryId" = major_category."MajorCategoriesId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_JobPosts_MajorCategoryId",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "MajorCategoryId",
                table: "JobPosts");

            migrationBuilder.AddColumn<Guid>(
                name: "MajorId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Categories" AS category
                SET "MajorId" = major_category."MajorId"
                FROM "MajorCategories" AS major_category
                WHERE category."CategoriesId" = major_category."CategoryId";

                UPDATE "Categories"
                SET "MajorId" = '11111111-1111-1111-1111-111111111111'
                WHERE "MajorId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MajorId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_MajorId",
                table: "Categories",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_CategoryId",
                table: "JobPosts",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Majors_MajorId",
                table: "Categories",
                column: "MajorId",
                principalTable: "Majors",
                principalColumn: "MajorsId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "JobPosts_CategoryId_fkey",
                table: "JobPosts",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");

            migrationBuilder.DropTable(
                name: "MajorCategories");
        }
    }
}
