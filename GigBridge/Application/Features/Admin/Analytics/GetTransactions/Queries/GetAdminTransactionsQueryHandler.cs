using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetTransactions.Queries;

public sealed class GetAdminTransactionsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetAdminTransactionsQuery, AdminTransactionPage>
{
    public Task<AdminTransactionPage> Handle(GetAdminTransactionsQuery request, CancellationToken cancellationToken) =>
        analytics.GetTransactionsAsync(request.Filter, cancellationToken);
}
