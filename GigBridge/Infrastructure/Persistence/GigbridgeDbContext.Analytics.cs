using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public partial class GigbridgeDbContext
{
    public DbSet<PlatformRevenueEvent> PlatformRevenueEvents => Set<PlatformRevenueEvent>();
    public DbSet<PremiumUsageEvent> PremiumUsageEvents => Set<PremiumUsageEvent>();
    public DbSet<MarketplaceAnalyticsEvent> MarketplaceAnalyticsEvents => Set<MarketplaceAnalyticsEvent>();
    public DbSet<MarketplaceAnalyticsDailyAggregate> MarketplaceAnalyticsDailyAggregates => Set<MarketplaceAnalyticsDailyAggregate>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        AnalyticsModelConfiguration.Configure(modelBuilder);
    }
}

internal static class AnalyticsModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlatformRevenueEvent>(entity =>
        {
            entity.ToTable("PlatformRevenueEvents");
            entity.HasKey(x => x.PlatformRevenueEventId).HasName("PlatformRevenueEvents_pkey");
            entity.Property(x => x.PlatformRevenueEventId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.GigCoinAmount).HasPrecision(18, 4);
            entity.Property(x => x.VndEquivalent).HasPrecision(18, 2);
            entity.Property(x => x.VndPerGigCoin).HasPrecision(18, 4);
            entity.Property(x => x.SourceEntityType).HasMaxLength(64);
            entity.Property(x => x.SourceReference).HasMaxLength(240);
            entity.Property(x => x.Metadata).HasColumnType("jsonb");
            entity.HasIndex(x => x.WalletTransactionId, "UX_PlatformRevenueEvents_WalletTransactionId").IsUnique().HasFilter("\"WalletTransactionId\" IS NOT NULL");
            entity.HasIndex(x => x.WalletWithdrawalId, "UX_PlatformRevenueEvents_WalletWithdrawalId").IsUnique().HasFilter("\"WalletWithdrawalId\" IS NOT NULL");
            entity.HasIndex(x => new { x.Source, x.OccurredAt }, "IX_PlatformRevenueEvents_Source_OccurredAt");
            entity.HasIndex(x => new { x.PayerUserId, x.OccurredAt }, "IX_PlatformRevenueEvents_PayerUserId_OccurredAt");
            entity.HasIndex(x => new { x.ContractId, x.OccurredAt }, "IX_PlatformRevenueEvents_ContractId_OccurredAt");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_PlatformRevenueEvents_Origin", "(\"WalletTransactionId\" IS NOT NULL)::integer + (\"WalletWithdrawalId\" IS NOT NULL)::integer = 1");
                table.HasCheckConstraint("CK_PlatformRevenueEvents_GigCoinAmount", "\"GigCoinAmount\" >= 0");
                table.HasCheckConstraint("CK_PlatformRevenueEvents_VndEquivalent", "\"VndEquivalent\" >= 0");
                table.HasCheckConstraint("CK_PlatformRevenueEvents_Rate", "\"VndPerGigCoin\" > 0");
            });
            entity.HasOne(x => x.WalletTransaction).WithMany().HasForeignKey(x => x.WalletTransactionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PlatformRevenueEvents_WalletTransactions_WalletTransactionId");
            entity.HasOne(x => x.WalletWithdrawal).WithMany().HasForeignKey(x => x.WalletWithdrawalId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PlatformRevenueEvents_WalletWithdrawals_WalletWithdrawalId");
            entity.HasOne(x => x.PayerUser).WithMany().HasForeignKey(x => x.PayerUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PlatformRevenueEvents_Users_PayerUserId");
            entity.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PlatformRevenueEvents_Contracts_ContractId");
        });

        modelBuilder.Entity<PremiumUsageEvent>(entity =>
        {
            entity.ToTable("PremiumUsageEvents");
            entity.HasKey(x => x.PremiumUsageEventId).HasName("PremiumUsageEvents_pkey");
            entity.Property(x => x.PremiumUsageEventId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.IdempotencyKey).HasMaxLength(240);
            entity.Property(x => x.Metadata).HasColumnType("jsonb");
            entity.HasIndex(x => x.IdempotencyKey, "UX_PremiumUsageEvents_IdempotencyKey").IsUnique();
            entity.HasIndex(x => new { x.Type, x.OccurredAt }, "IX_PremiumUsageEvents_Type_OccurredAt");
            entity.HasIndex(x => new { x.UserId, x.OccurredAt }, "IX_PremiumUsageEvents_UserId_OccurredAt");
            entity.HasIndex(x => x.JobPostId, "IX_PremiumUsageEvents_JobPostId");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_PremiumUsageEvents_Users_UserId");
            entity.HasOne(x => x.JobPost).WithMany().HasForeignKey(x => x.JobPostId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_PremiumUsageEvents_JobPosts_JobPostId");
        });

        modelBuilder.Entity<MarketplaceAnalyticsEvent>(entity =>
        {
            entity.ToTable("MarketplaceAnalyticsEvents");
            entity.HasKey(x => x.MarketplaceAnalyticsEventId).HasName("MarketplaceAnalyticsEvents_pkey");
            entity.Property(x => x.MarketplaceAnalyticsEventId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ActorKey).HasMaxLength(64);
            entity.Property(x => x.DedupeKey).HasMaxLength(128);
            entity.Property(x => x.NormalizedQuery).HasMaxLength(120);
            entity.Property(x => x.FilterMetadata).HasColumnType("jsonb");
            entity.HasIndex(x => x.DedupeKey, "UX_MarketplaceAnalyticsEvents_DedupeKey").IsUnique();
            entity.HasIndex(x => new { x.Type, x.OccurredAt }, "IX_MarketplaceAnalyticsEvents_Type_OccurredAt");
            entity.HasIndex(x => new { x.NormalizedQuery, x.OccurredAt }, "IX_MarketplaceAnalyticsEvents_Query_OccurredAt").HasFilter("\"NormalizedQuery\" IS NOT NULL");
            entity.HasIndex(x => new { x.JobPostId, x.Type, x.OccurredAt }, "IX_MarketplaceAnalyticsEvents_Job_Type_OccurredAt").HasFilter("\"JobPostId\" IS NOT NULL");
            entity.HasIndex(x => x.SearchEventId, "IX_MarketplaceAnalyticsEvents_SearchEventId").HasFilter("\"SearchEventId\" IS NOT NULL");
            entity.ToTable(table => table.HasCheckConstraint("CK_MarketplaceAnalyticsEvents_ResultCount", "\"ResultCount\" IS NULL OR \"ResultCount\" >= 0"));
            entity.HasOne(x => x.JobPost).WithMany().HasForeignKey(x => x.JobPostId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_MarketplaceAnalyticsEvents_JobPosts_JobPostId");
        });

        modelBuilder.Entity<MarketplaceAnalyticsDailyAggregate>(entity =>
        {
            entity.ToTable("MarketplaceAnalyticsDailyAggregates");
            entity.HasKey(x => x.MarketplaceAnalyticsDailyAggregateId).HasName("MarketplaceAnalyticsDailyAggregates_pkey");
            entity.Property(x => x.MarketplaceAnalyticsDailyAggregateId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.DimensionType).HasMaxLength(24);
            entity.Property(x => x.DimensionKey).HasMaxLength(160);
            entity.Property(x => x.Label).HasMaxLength(160);
            entity.HasIndex(x => new { x.Date, x.DimensionType, x.DimensionKey }, "UX_MarketplaceAnalyticsDailyAggregates_Dimension").IsUnique();
            entity.HasIndex(x => new { x.DimensionType, x.Date }, "IX_MarketplaceAnalyticsDailyAggregates_Type_Date");
        });
    }
}
