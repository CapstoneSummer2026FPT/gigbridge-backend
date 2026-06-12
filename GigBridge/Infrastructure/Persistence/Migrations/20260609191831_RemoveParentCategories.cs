using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParentCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Categories_ParentCategoryId_fkey",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "ParentCategoryId",
                table: "Categories",
                newName: "ParentCategoryCategoriesId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                newName: "IX_Categories_ParentCategoryCategoriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryCategoriesId",
                table: "Categories",
                column: "ParentCategoryCategoriesId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryCategoriesId",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "ParentCategoryCategoriesId",
                table: "Categories",
                newName: "ParentCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_ParentCategoryCategoriesId",
                table: "Categories",
                newName: "IX_Categories_ParentCategoryId");

            migrationBuilder.AddForeignKey(
                name: "Categories_ParentCategoryId_fkey",
                table: "Categories",
                column: "ParentCategoryId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");
        }
    }
}
