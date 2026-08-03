using MediatR;

namespace Application.Features.Wallets.Common.GetTransactions.Queries;

public sealed record GetWalletTransactionsSummaryQuery(Guid UserId)
    : IRequest<WalletTransactionsSummaryResponse>;
