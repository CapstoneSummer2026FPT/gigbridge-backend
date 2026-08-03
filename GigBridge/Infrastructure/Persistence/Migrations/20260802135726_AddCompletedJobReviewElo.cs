using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedJobReviewElo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration, 7=CompletedJobReview",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration");

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "UserEloPointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rating",
                table: "UserEloPointTransactions",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewId",
                table: "UserEloPointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rating",
                table: "Reviews",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_UserId_ContractId_CompletedJobReview",
                table: "UserEloPointTransactions",
                columns: new[] { "UserId", "ContractId" },
                unique: true,
                filter: "\"Reason\" = 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserEloPointTransactions_UserId_ContractId_CompletedJobReview",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "ReviewId",
                table: "UserEloPointTransactions");

            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration, 7=CompletedJobReview");

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "Reviews",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(3,1)",
                oldPrecision: 3,
                oldScale: 1);
        }
    }
}
