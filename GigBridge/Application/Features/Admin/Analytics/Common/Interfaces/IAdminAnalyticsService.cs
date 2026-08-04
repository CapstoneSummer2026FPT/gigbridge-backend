using Application.Features.Admin.Analytics.Common.DTOs;

namespace Application.Features.Admin.Analytics.Common.Interfaces;

public interface IAdminAnalyticsService
{
    Task<FinanceAnalyticsResponse> GetFinanceAsync(AdminAnalyticsRangeRequest request, CancellationToken cancellationToken);
    Task<PremiumAnalyticsResponse> GetPremiumAsync(AdminAnalyticsRangeRequest request, CancellationToken cancellationToken);
    Task<AdminTransactionPage> GetTransactionsAsync(AdminTransactionFilter filter, CancellationToken cancellationToken);
    Task<string> ExportTransactionsAsync(AdminTransactionFilter filter, CancellationToken cancellationToken);
    Task<MarketplaceAnalyticsResponse> GetMarketplaceAsync(AdminAnalyticsRangeRequest request, CancellationToken cancellationToken);
}
