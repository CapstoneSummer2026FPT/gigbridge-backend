using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.GetDetail;

public sealed record GetWithdrawalDetailQuery(
    Guid UserId,
    Guid WithdrawalId) : IRequest<WithdrawalResponse>;
