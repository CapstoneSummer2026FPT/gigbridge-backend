using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed record AdminMarkWithdrawalFailedCommand(
    Guid AdminUserId,
    Guid WithdrawalId,
    AdminMarkWithdrawalFailedRequest Request) : IRequest<WithdrawalResponse>;
