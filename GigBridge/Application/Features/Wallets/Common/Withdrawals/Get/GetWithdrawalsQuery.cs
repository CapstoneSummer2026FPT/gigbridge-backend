using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Get;

public sealed record GetWithdrawalsQuery(Guid UserId, int Limit = 50) : IRequest<IReadOnlyList<WithdrawalResponse>>;
