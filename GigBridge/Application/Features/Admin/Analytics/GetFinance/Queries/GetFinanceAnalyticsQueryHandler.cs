using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetFinance.Queries;

public sealed class GetFinanceAnalyticsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetFinanceAnalyticsQuery, FinanceAnalyticsResponse>
{
    public Task<FinanceAnalyticsResponse> Handle(GetFinanceAnalyticsQuery request, CancellationToken cancellationToken) =>
        analytics.GetFinanceAsync(request.Range, cancellationToken);
}
