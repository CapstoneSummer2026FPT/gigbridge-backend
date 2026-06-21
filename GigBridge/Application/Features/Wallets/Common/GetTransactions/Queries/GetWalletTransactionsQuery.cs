using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.GetTransactions.Queries;

public sealed record GetWalletTransactionsQuery(Guid UserId, int Limit = 50) : IRequest<IReadOnlyList<WalletTransactionResponse>>;
