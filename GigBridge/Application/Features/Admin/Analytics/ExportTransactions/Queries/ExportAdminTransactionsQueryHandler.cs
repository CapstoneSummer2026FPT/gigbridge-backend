using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Services;
using MediatR;

namespace Application.Features.Admin.Analytics.ExportTransactions.Queries;

public sealed class ExportAdminTransactionsQueryHandler(IAdminAnalyticsService analytics)
    : IRequestHandler<ExportAdminTransactionsQuery, string>
{
    public Task<string> Handle(ExportAdminTransactionsQuery request, CancellationToken cancellationToken) =>
        analytics.ExportTransactionsAsync(request.Filter, cancellationToken);
}
