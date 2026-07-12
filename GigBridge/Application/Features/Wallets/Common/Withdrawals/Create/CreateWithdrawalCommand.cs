using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Create;

public sealed record CreateWithdrawalCommand(
    Guid UserId,
    CreateWithdrawalRequest Request) : IRequest<WithdrawalResponse>;
