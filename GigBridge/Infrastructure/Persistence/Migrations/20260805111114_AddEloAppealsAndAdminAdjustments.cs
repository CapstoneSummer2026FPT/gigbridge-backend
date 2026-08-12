using Domain.Enums.Elo;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEloAppealsAndAdminAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration, 7=CompletedJobReview, 8=DisputeResolutionPenalty, 9=AdminIncrease, 10=AdminDecrease, 11=AppealCorrection, 12=Reversal, 13=SystemAdjustment",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration, 7=CompletedJobReview");

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedByAdminId",
                table: "UserEloPointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EloAppealId",
                table: "UserEloPointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: true,
                comment: "Enum EloAdjustmentMode: 0=FixedPoints, 1=Percentage");

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: true,
                comment: "Enum EloAdjustmentSourceType: 0=Review, 1=Dispute, 2=EloAppeal, 3=Admin, 4=System");

            migrationBuilder.CreateTable(
                name: "EloPointAppeals",
                columns: table => new
                {
                    EloPointAppealId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EloPointTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum EloPointAppealStatus: 0=Pending, 1=UnderReview, 2=Approved, 3=PartiallyApproved, 4=Rejected, 5=Cancelled"),
                    Resolution = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectedDelta = table.Column<int>(type: "integer", nullable: true),
                    AppliedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("EloPointAppeals_pkey", x => x.EloPointAppealId);
                    table.ForeignKey(
                        name: "EloPointAppeals_elo_AppliedTransactionId_fkey",
                        column: x => x.AppliedTransactionId,
                        principalTable: "UserEloPointTransactions",
                        principalColumn: "UserEloPointTransactionsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "EloPointAppeals_elo_EloPointTransactionId_fkey",
                        column: x => x.EloPointTransactionId,
                        principalTable: "UserEloPointTransactions",
                        principalColumn: "UserEloPointTransactionsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "EloPointAppeals_usr_ReviewedByAdminId_fkey",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "EloPointAppeals_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "EloPointAppealEvidence",
                columns: table => new
                {
                    EloPointAppealEvidenceId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EloPointAppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("EloPointAppealEvidence_pkey", x => x.EloPointAppealEvidenceId);
                    table.ForeignKey(
                        name: "EloPointAppealEvidence_elo_EloPointAppealId_fkey",
                        column: x => x.EloPointAppealId,
                        principalTable: "EloPointAppeals",
                        principalColumn: "EloPointAppealId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "EloPointAppealEvidence_usr_UploadedById_fkey",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_AppliedByAdminId",
                table: "UserEloPointTransactions",
                column: "AppliedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_EloAppealId",
                table: "UserEloPointTransactions",
                column: "EloAppealId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEloPointTransactions_UserId_SourceType",
                table: "UserEloPointTransactions",
                columns: new[] { "UserId", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppealEvidence_AppealId",
                table: "EloPointAppealEvidence",
                column: "EloPointAppealId");

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppealEvidence_UploadedById",
                table: "EloPointAppealEvidence",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppeals_AppliedTransactionId",
                table: "EloPointAppeals",
                column: "AppliedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppeals_EloPointTransactionId",
                table: "EloPointAppeals",
                column: "EloPointTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppeals_ReviewedByAdminId",
                table: "EloPointAppeals",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppeals_Status_CreatedAt",
                table: "EloPointAppeals",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppeals_UserId_Status",
                table: "EloPointAppeals",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EloPointAppeals_UserId_Transaction_Active",
                table: "EloPointAppeals",
                columns: new[] { "UserId", "EloPointTransactionId" },
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.AddForeignKey(
                name: "UserEloPointTransactions_elo_EloAppealId_fkey",
                table: "UserEloPointTransactions",
                column: "EloAppealId",
                principalTable: "EloPointAppeals",
                principalColumn: "EloPointAppealId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "UserEloPointTransactions_usr_AppliedByAdminId_fkey",
                table: "UserEloPointTransactions",
                column: "AppliedByAdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            // Backfill SourceType for pre-existing transactions so the new column is
            // never null for legacy rows. Mapping matches the enum + reason categories:
            // ReviewRating/ReviewModeration/CompletedJobReview => Review(0),
            // DisputeResolutionPenalty => Dispute(1),
            // InitialGrant/InactivityPenalty/ReturnBonus/JobCompletion/LegacyIntegrityPenalty => System(4).
            migrationBuilder.Sql(
                """
                UPDATE "UserEloPointTransactions"
                SET "SourceType" = CASE
                    WHEN "Reason" IN (4, 6, 7) THEN 0
                    WHEN "Reason" = 8 THEN 1
                    WHEN "Reason" IN (0, 1, 2, 3, 5) THEN 4
                    ELSE "SourceType"
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "UserEloPointTransactions_elo_EloAppealId_fkey",
                table: "UserEloPointTransactions");

            migrationBuilder.DropForeignKey(
                name: "UserEloPointTransactions_usr_AppliedByAdminId_fkey",
                table: "UserEloPointTransactions");

            migrationBuilder.DropTable(
                name: "EloPointAppealEvidence");

            migrationBuilder.DropTable(
                name: "EloPointAppeals");

            migrationBuilder.DropIndex(
                name: "IX_UserEloPointTransactions_AppliedByAdminId",
                table: "UserEloPointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_UserEloPointTransactions_EloAppealId",
                table: "UserEloPointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_UserEloPointTransactions_UserId_SourceType",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "AppliedByAdminId",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "EloAppealId",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "UserEloPointTransactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "UserEloPointTransactions");

            migrationBuilder.AlterColumn<int>(
                name: "Reason",
                table: "UserEloPointTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration, 7=CompletedJobReview",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration, 7=CompletedJobReview, 8=DisputeResolutionPenalty, 9=AdminIncrease, 10=AdminDecrease, 11=AppealCorrection, 12=Reversal, 13=SystemAdjustment");
        }
    }
}
