using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260802171000_AddAnalyticsTables")]
public partial class AddAnalyticsTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MarketplaceAnalyticsDailyAggregates",
            columns: table => new
            {
                MarketplaceAnalyticsDailyAggregateId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                DimensionType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                DimensionKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                SearchCount = table.Column<long>(type: "bigint", nullable: false),
                DistinctActorCount = table.Column<long>(type: "bigint", nullable: false),
                ZeroResultCount = table.Column<long>(type: "bigint", nullable: false),
                ResultCountTotal = table.Column<long>(type: "bigint", nullable: false),
                ViewCount = table.Column<long>(type: "bigint", nullable: false),
                SaveCount = table.Column<long>(type: "bigint", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("MarketplaceAnalyticsDailyAggregates_pkey", x => x.MarketplaceAnalyticsDailyAggregateId);
            });

        migrationBuilder.CreateTable(
            name: "MarketplaceAnalyticsEvents",
            columns: table => new
            {
                MarketplaceAnalyticsEventId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                Type = table.Column<int>(type: "integer", nullable: false),
                ActorKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                DedupeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                NormalizedQuery = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                JobPostId = table.Column<Guid>(type: "uuid", nullable: true),
                SearchEventId = table.Column<Guid>(type: "uuid", nullable: true),
                ResultCount = table.Column<int>(type: "integer", nullable: true),
                FilterMetadata = table.Column<string>(type: "jsonb", nullable: true),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("MarketplaceAnalyticsEvents_pkey", x => x.MarketplaceAnalyticsEventId);
                table.CheckConstraint("CK_MarketplaceAnalyticsEvents_ResultCount", "\"ResultCount\" IS NULL OR \"ResultCount\" >= 0");
                table.ForeignKey(
                    name: "FK_MarketplaceAnalyticsEvents_JobPosts_JobPostId",
                    column: x => x.JobPostId,
                    principalTable: "JobPosts",
                    principalColumn: "JobPostsId",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "PlatformRevenueEvents",
            columns: table => new
            {
                PlatformRevenueEventId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                Source = table.Column<int>(type: "integer", nullable: false),
                WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                WalletWithdrawalId = table.Column<Guid>(type: "uuid", nullable: true),
                PayerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                SourceEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SourceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                SourceReference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                GigCoinAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                VndEquivalent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                VndPerGigCoin = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsBackfilled = table.Column<bool>(type: "boolean", nullable: false),
                Metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PlatformRevenueEvents_pkey", x => x.PlatformRevenueEventId);
                table.CheckConstraint("CK_PlatformRevenueEvents_GigCoinAmount", "\"GigCoinAmount\" >= 0");
                table.CheckConstraint("CK_PlatformRevenueEvents_Origin", "(\"WalletTransactionId\" IS NOT NULL)::integer + (\"WalletWithdrawalId\" IS NOT NULL)::integer = 1");
                table.CheckConstraint("CK_PlatformRevenueEvents_Rate", "\"VndPerGigCoin\" > 0");
                table.CheckConstraint("CK_PlatformRevenueEvents_VndEquivalent", "\"VndEquivalent\" >= 0");
                table.ForeignKey(
                    name: "FK_PlatformRevenueEvents_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "ContractsId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlatformRevenueEvents_Users_PayerUserId",
                    column: x => x.PayerUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlatformRevenueEvents_WalletTransactions_WalletTransactionId",
                    column: x => x.WalletTransactionId,
                    principalTable: "WalletTransactions",
                    principalColumn: "WalletTransactionsId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlatformRevenueEvents_WalletWithdrawals_WalletWithdrawalId",
                    column: x => x.WalletWithdrawalId,
                    principalTable: "WalletWithdrawals",
                    principalColumn: "WalletWithdrawalId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PremiumUsageEvents",
            columns: table => new
            {
                PremiumUsageEventId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                Type = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                JobPostId = table.Column<Guid>(type: "uuid", nullable: true),
                PromotionId = table.Column<Guid>(type: "uuid", nullable: true),
                IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PremiumUsageEvents_pkey", x => x.PremiumUsageEventId);
                table.ForeignKey(
                    name: "FK_PremiumUsageEvents_JobPosts_JobPostId",
                    column: x => x.JobPostId,
                    principalTable: "JobPosts",
                    principalColumn: "JobPostsId",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PremiumUsageEvents_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MarketplaceAnalyticsDailyAggregates_Type_Date",
            table: "MarketplaceAnalyticsDailyAggregates",
            columns: new[] { "DimensionType", "Date" });

        migrationBuilder.CreateIndex(
            name: "UX_MarketplaceAnalyticsDailyAggregates_Dimension",
            table: "MarketplaceAnalyticsDailyAggregates",
            columns: new[] { "Date", "DimensionType", "DimensionKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_MarketplaceAnalyticsEvents_DedupeKey",
            table: "MarketplaceAnalyticsEvents",
            column: "DedupeKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MarketplaceAnalyticsEvents_Job_Type_OccurredAt",
            table: "MarketplaceAnalyticsEvents",
            columns: new[] { "JobPostId", "Type", "OccurredAt" },
            filter: "\"JobPostId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_MarketplaceAnalyticsEvents_Query_OccurredAt",
            table: "MarketplaceAnalyticsEvents",
            columns: new[] { "NormalizedQuery", "OccurredAt" },
            filter: "\"NormalizedQuery\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_MarketplaceAnalyticsEvents_SearchEventId",
            table: "MarketplaceAnalyticsEvents",
            column: "SearchEventId",
            filter: "\"SearchEventId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_MarketplaceAnalyticsEvents_Type_OccurredAt",
            table: "MarketplaceAnalyticsEvents",
            columns: new[] { "Type", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlatformRevenueEvents_ContractId_OccurredAt",
            table: "PlatformRevenueEvents",
            columns: new[] { "ContractId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlatformRevenueEvents_PayerUserId_OccurredAt",
            table: "PlatformRevenueEvents",
            columns: new[] { "PayerUserId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlatformRevenueEvents_Source_OccurredAt",
            table: "PlatformRevenueEvents",
            columns: new[] { "Source", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "UX_PlatformRevenueEvents_WalletTransactionId",
            table: "PlatformRevenueEvents",
            column: "WalletTransactionId",
            unique: true,
            filter: "\"WalletTransactionId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "UX_PlatformRevenueEvents_WalletWithdrawalId",
            table: "PlatformRevenueEvents",
            column: "WalletWithdrawalId",
            unique: true,
            filter: "\"WalletWithdrawalId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PremiumUsageEvents_JobPostId",
            table: "PremiumUsageEvents",
            column: "JobPostId");

        migrationBuilder.CreateIndex(
            name: "UX_PremiumUsageEvents_IdempotencyKey",
            table: "PremiumUsageEvents",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PremiumUsageEvents_Type_OccurredAt",
            table: "PremiumUsageEvents",
            columns: new[] { "Type", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PremiumUsageEvents_UserId_OccurredAt",
            table: "PremiumUsageEvents",
            columns: new[] { "UserId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PremiumUsageEvents");
        migrationBuilder.DropTable(name: "PlatformRevenueEvents");
        migrationBuilder.DropTable(name: "MarketplaceAnalyticsEvents");
        migrationBuilder.DropTable(name: "MarketplaceAnalyticsDailyAggregates");
    }
}
