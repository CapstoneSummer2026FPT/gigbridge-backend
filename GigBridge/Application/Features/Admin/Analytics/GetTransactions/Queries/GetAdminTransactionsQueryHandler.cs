using Application.Common.InternalServices.Admin.Analytics.Models;
using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.GetTransactions.Queries;

public sealed class GetAdminTransactionsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<GetAdminTransactionsQuery, AdminTransactionPage>
{
    public Task<AdminTransactionPage> Handle(GetAdminTransactionsQuery request, CancellationToken cancellationToken) =>
        analytics.GetTransactionsAsync(request.Filter, cancellationToken);
}
