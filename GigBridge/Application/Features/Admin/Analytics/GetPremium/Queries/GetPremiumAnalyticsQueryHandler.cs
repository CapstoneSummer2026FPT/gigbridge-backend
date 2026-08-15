using Application.Common.InternalServices.Admin.Analytics.Models;
using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetPremium.Queries;

public sealed class GetPremiumAnalyticsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetPremiumAnalyticsQuery, PremiumAnalyticsResponse>
{
    public Task<PremiumAnalyticsResponse> Handle(GetPremiumAnalyticsQuery request, CancellationToken cancellationToken) =>
        analytics.GetPremiumAsync(request.Range, cancellationToken);
}
