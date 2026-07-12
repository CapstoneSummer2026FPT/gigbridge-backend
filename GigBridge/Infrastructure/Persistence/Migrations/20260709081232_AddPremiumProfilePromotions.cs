using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumProfilePromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FreelancerProfilePromotions",
                columns: table => new
                {
                    FreelancerProfilePromotionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    TokenCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BoostWeight = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImpressionCount = table.Column<int>(type: "integer", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("FreelancerProfilePromotions_pkey", x => x.FreelancerProfilePromotionsId);
                    table.ForeignKey(
                        name: "FK_FreelancerProfilePromotions_FreelancerProfiles_FreelancerPr~",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FreelancerProfilePromotions_WalletTransactions_WalletTransa~",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "WalletTransactionsId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfilePromotions_End",
                table: "FreelancerProfilePromotions",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfilePromotions_Queue",
                table: "FreelancerProfilePromotions",
                columns: new[] { "FreelancerProfileId", "Status", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfilePromotions_WalletTransactionId",
                table: "FreelancerProfilePromotions",
                column: "WalletTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FreelancerProfilePromotions_OneActive",
                table: "FreelancerProfilePromotions",
                column: "FreelancerProfileId",
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreelancerProfilePromotions");
        }
    }
}
