using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Sync;

public sealed record SyncWithdrawalCommand(
    Guid WithdrawalId,
    Guid? UserId,
    bool IsAdmin) : IRequest<WithdrawalResponse>;
