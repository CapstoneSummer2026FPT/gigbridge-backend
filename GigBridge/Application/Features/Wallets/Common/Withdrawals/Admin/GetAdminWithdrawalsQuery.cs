using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed record GetAdminWithdrawalsQuery(
    int? Status,
    int Limit = 100) : IRequest<IReadOnlyList<WithdrawalResponse>>;
