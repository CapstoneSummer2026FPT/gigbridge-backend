using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

/// <param name="Status">
/// Optional withdrawal status filter. Omit to sync every non-terminal withdrawal.
/// </param>
public sealed record BulkSyncWithdrawalsCommand(int? Status, int Limit)
    : IRequest<BulkWithdrawalOperationResponse>;
