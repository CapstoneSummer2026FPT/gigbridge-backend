using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowNewContractAfterCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts",
                column: "JobPostsId",
                unique: true,
                filter: "\"Status\" <> 9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts",
                column: "JobPostsId",
                unique: true);
        }
    }
}
