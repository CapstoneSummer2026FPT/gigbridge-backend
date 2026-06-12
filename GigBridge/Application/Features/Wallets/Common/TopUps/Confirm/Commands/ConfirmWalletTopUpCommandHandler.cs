using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.TopUps.Confirm.Commands;

public sealed class ConfirmWalletTopUpCommandHandler :
    IRequestHandler<ConfirmWalletTopUpCommand, WalletTransactionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IWalletTopUpPaymentService _paymentService;

    public ConfirmWalletTopUpCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IWalletTopUpPaymentService paymentService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _paymentService = paymentService;
    }

    public async Task<WalletTransactionResponse> Handle(
        ConfirmWalletTopUpCommand command,
        CancellationToken cancellationToken)
    {
        var callback = command.Request;
        var payload = new WalletTopUpCallbackPayload(
            callback.Data?.OrderCode ?? callback.OrderCode,
            callback.Success == true || callback.Code == "00",
            callback.Data?.Reference ?? callback.Data?.PaymentLinkId ?? callback.GatewayTransactionCode,
            callback.Data?.Amount ?? callback.AmountVnd,
            callback.Data?.Desc ?? callback.Desc,
            callback.Signature,
            callback.Data?.ToSignatureData() ?? new Dictionary<string, string?>());

        var verified = await _paymentService.VerifyCallbackAsync(payload, cancellationToken);
        if (!verified.IsVerified)
        {
            throw new BadRequestException("PayOS callback signature is invalid.");
        }

        if (!verified.OrderCode.HasValue)
        {
            throw new BadRequestException("PayOS callback is missing order code.");
        }

        var orderCode = verified.OrderCode.Value.ToString();
        var transaction = await _context.Set<WalletTransaction>()
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.Type == (int)WalletTransactionType.TopUp &&
                    transaction.GatewayOrderCode == orderCode,
                cancellationToken);

        if (transaction is null)
        {
            throw new NotFoundException("Wallet top-up transaction does not exist.");
        }

        if (transaction.Status == (int)WalletTransactionStatus.Succeeded)
        {
            return WalletTransactionResponse.FromEntity(transaction);
        }

        if (transaction.Status == (int)WalletTransactionStatus.Failed ||
            transaction.Status == (int)WalletTransactionStatus.Cancelled)
        {
            return WalletTransactionResponse.FromEntity(transaction);
        }

        if (!verified.IsSucceeded)
        {
            transaction.Status = (int)WalletTransactionStatus.Failed;
            transaction.Note = verified.FailureReason;
            transaction.CompletedAt = _dateTimeService.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return WalletTransactionResponse.FromEntity(transaction);
        }

        if (verified.AmountVnd.HasValue && verified.AmountVnd.Value != transaction.VndAmount)
        {
            throw new BadRequestException("PayOS callback amount does not match the pending top-up.");
        }

        var wallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserWalletsId == transaction.UserWalletsId, cancellationToken);

        if (wallet is null)
        {
            throw new NotFoundException("Wallet does not exist.");
        }

        var now = _dateTimeService.UtcNow;
        wallet.AvailableTokens += transaction.TokenAmount;
        wallet.UpdatedAt = now;

        transaction.Status = (int)WalletTransactionStatus.Succeeded;
        transaction.GatewayTransactionCode = verified.GatewayTransactionCode ?? transaction.GatewayTransactionCode;
        transaction.CompletedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return WalletTransactionResponse.FromEntity(transaction);
    }
}
