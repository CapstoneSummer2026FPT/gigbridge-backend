using Application.Common.InternalServices.Admin.Analytics.Models;
using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetMarketplace.Queries;

public sealed class GetMarketplaceAnalyticsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetMarketplaceAnalyticsQuery, MarketplaceAnalyticsResponse>
{
    public Task<MarketplaceAnalyticsResponse> Handle(GetMarketplaceAnalyticsQuery request, CancellationToken cancellationToken) =>
        analytics.GetMarketplaceAsync(request.Range, cancellationToken);
}
