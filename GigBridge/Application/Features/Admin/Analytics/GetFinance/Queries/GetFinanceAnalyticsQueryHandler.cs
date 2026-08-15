using Application.Common.InternalServices.Admin.Analytics.Models;
using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetFinance.Queries;

public sealed class GetFinanceAnalyticsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetFinanceAnalyticsQuery, FinanceAnalyticsResponse>
{
    public Task<FinanceAnalyticsResponse> Handle(GetFinanceAnalyticsQuery request, CancellationToken cancellationToken) =>
        analytics.GetFinanceAsync(request.Range, cancellationToken);
}
