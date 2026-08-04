using Application.Features.Admin.Analytics.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Analytics.ExportTransactions.Queries;

public sealed record ExportAdminTransactionsQuery(AdminTransactionFilter Filter) : IRequest<string>;
