using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed record GetAdminWithdrawalDetailQuery(Guid WithdrawalId) : IRequest<WithdrawalResponse>;
