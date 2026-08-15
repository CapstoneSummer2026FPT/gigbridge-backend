using Application.Common.InternalServices.Admin.Analytics.Models;

namespace Application.Common.InternalServices.Admin.Analytics.Interfaces;
public interface IAdminAnalyticsService
{
    Task<FinanceAnalyticsResponse> GetFinanceAsync(AdminAnalyticsRangeRequest request, CancellationToken cancellationToken);
    Task<PremiumAnalyticsResponse> GetPremiumAsync(AdminAnalyticsRangeRequest request, CancellationToken cancellationToken);
    Task<AdminTransactionPage> GetTransactionsAsync(AdminTransactionFilter filter, CancellationToken cancellationToken);
    Task<string> ExportTransactionsAsync(AdminTransactionFilter filter, CancellationToken cancellationToken);
    Task<MarketplaceAnalyticsResponse> GetMarketplaceAsync(AdminAnalyticsRangeRequest request, CancellationToken cancellationToken);
}
