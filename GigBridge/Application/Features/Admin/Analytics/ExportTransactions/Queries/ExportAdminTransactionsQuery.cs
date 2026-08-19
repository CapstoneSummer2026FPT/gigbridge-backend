using Application.Common.InternalServices.Admin.Analytics.Models;
using MediatR;

namespace Application.Features.Admin.Analytics.ExportTransactions.Queries;

public sealed record ExportAdminTransactionsQuery(AdminTransactionFilter Filter) : IRequest<string>;
