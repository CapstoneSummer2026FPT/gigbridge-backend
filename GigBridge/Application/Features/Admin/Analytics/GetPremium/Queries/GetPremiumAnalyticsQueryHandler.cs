using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetPremium.Queries;

public sealed class GetPremiumAnalyticsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetPremiumAnalyticsQuery, PremiumAnalyticsResponse>
{
    public Task<PremiumAnalyticsResponse> Handle(GetPremiumAnalyticsQuery request, CancellationToken cancellationToken) =>
        analytics.GetPremiumAsync(request.Range, cancellationToken);
}
