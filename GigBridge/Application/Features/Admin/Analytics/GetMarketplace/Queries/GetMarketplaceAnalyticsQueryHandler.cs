using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetMarketplace.Queries;

public sealed class GetMarketplaceAnalyticsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetMarketplaceAnalyticsQuery, MarketplaceAnalyticsResponse>
{
    public Task<MarketplaceAnalyticsResponse> Handle(GetMarketplaceAnalyticsQuery request, CancellationToken cancellationToken) =>
        analytics.GetMarketplaceAsync(request.Range, cancellationToken);
}
