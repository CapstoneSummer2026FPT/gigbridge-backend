using Application.Features.Admin.AdminCredit.DTOs;
using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.AdminCredit.Commands;

public sealed record AdminWalletUpdateCommand(
    Guid AdminUserId,
    Guid TargetUserId,
    AdminUpdateWalletRequest Request) : IRequest<WalletTransactionResponse>;
