using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed record RetryWithdrawalCommand(
    Guid AdminUserId,
    Guid WithdrawalId) : IRequest<WithdrawalResponse>;
