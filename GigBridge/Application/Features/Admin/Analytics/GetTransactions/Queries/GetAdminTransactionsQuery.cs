using Application.Common.InternalServices.Admin.Analytics.Models;
using MediatR;

namespace Application.Features.Admin.Analytics.GetTransactions.Queries;

public sealed record GetAdminTransactionsQuery(AdminTransactionFilter Filter)
    : IRequest<AdminTransactionPage>;
