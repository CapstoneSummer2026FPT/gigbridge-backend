using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.TopUps.Sync.Commands;

public sealed class SyncWalletTopUpCommandHandler :
    IRequestHandler<SyncWalletTopUpCommand, WalletTransactionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IWalletTopUpPaymentService _paymentService;

    public SyncWalletTopUpCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IWalletTopUpPaymentService paymentService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _paymentService = paymentService;
    }

    public async Task<WalletTransactionResponse> Handle(
        SyncWalletTopUpCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request.OrderCode <= 0)
        {
            throw new BadRequestException("PayOS order code is invalid.");
        }

        var orderCode = command.Request.OrderCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var transaction = await _context.Set<WalletTransaction>()
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.UserId == command.UserId &&
                    transaction.Type == (int)WalletTransactionType.TopUp &&
                    transaction.GatewayOrderCode == orderCode,
                cancellationToken);

        if (transaction is null)
        {
            throw new NotFoundException("Wallet top-up transaction does not exist.");
        }

        if (transaction.Status == (int)WalletTransactionStatus.Succeeded ||
            transaction.Status == (int)WalletTransactionStatus.Failed ||
            transaction.Status == (int)WalletTransactionStatus.Cancelled)
        {
            return WalletTransactionResponse.FromEntity(transaction);
        }

        var paymentStatus = await _paymentService.GetPaymentStatusAsync(
            command.Request.OrderCode,
            cancellationToken);

        if (paymentStatus.OrderCode.HasValue &&
            paymentStatus.OrderCode.Value != command.Request.OrderCode)
        {
            throw new BadRequestException("PayOS payment status does not match the pending top-up.");
        }

        if (paymentStatus.AmountVnd.HasValue &&
            paymentStatus.AmountVnd.Value != transaction.VndAmount)
        {
            throw new BadRequestException("PayOS payment amount does not match the pending top-up.");
        }

        if (paymentStatus.IsCancelled || paymentStatus.IsFailed)
        {
            transaction.Status = paymentStatus.IsCancelled
                ? (int)WalletTransactionStatus.Cancelled
                : (int)WalletTransactionStatus.Failed;
            transaction.Note = paymentStatus.FailureReason;
            transaction.CompletedAt = _dateTimeService.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return WalletTransactionResponse.FromEntity(transaction);
        }

        if (!paymentStatus.IsSucceeded)
        {
            return WalletTransactionResponse.FromEntity(transaction);
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
        transaction.GatewayTransactionCode =
            paymentStatus.GatewayTransactionCode ?? transaction.GatewayTransactionCode;
        transaction.CompletedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return WalletTransactionResponse.FromEntity(transaction);
    }
}
