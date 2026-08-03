using Application.Features.Admin.Analytics.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Analytics.GetTransactions.Queries;

public sealed record GetAdminTransactionsQuery(AdminTransactionFilter Filter)
    : IRequest<AdminTransactionPage>;
