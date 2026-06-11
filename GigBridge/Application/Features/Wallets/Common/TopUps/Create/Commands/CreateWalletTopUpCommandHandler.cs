using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.TopUps.Create.Commands;

public sealed class CreateWalletTopUpCommandHandler :
    IRequestHandler<CreateWalletTopUpCommand, CreateWalletTopUpResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IWalletTopUpPaymentService _paymentService;

    public CreateWalletTopUpCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IWalletTopUpPaymentService paymentService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _paymentService = paymentService;
    }

    public async Task<CreateWalletTopUpResponse> Handle(
        CreateWalletTopUpCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request.TokenAmount <= 0)
        {
            throw new BadRequestException("Token amount must be greater than zero.");
        }

        if (!string.IsNullOrWhiteSpace(command.Request.IdempotencyKey))
        {
            var existing = await _context.Set<WalletTransaction>()
                .FirstOrDefaultAsync(
                    transaction =>
                        transaction.UserId == command.UserId &&
                        transaction.IdempotencyKey == command.Request.IdempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                return ToResponse(existing);
            }
        }

        var now = _dateTimeService.UtcNow;
        var wallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context,
            command.UserId,
            now,
            cancellationToken);

        var transaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.UserId,
            TokenAmount = command.Request.TokenAmount,
            VndAmount = TokenWalletRules.ToVnd(command.Request.TokenAmount),
            Type = (int)WalletTransactionType.TopUp,
            Status = (int)WalletTransactionStatus.Pending,
            IdempotencyKey = command.Request.IdempotencyKey,
            GatewayProvider = "PayOS",
            CreatedAt = now
        };

        _context.Set<WalletTransaction>().Add(transaction);

        var orderCode = GenerateOrderCode(now);
        var paymentResult = await _paymentService.CreatePaymentAsync(
            new WalletTopUpPaymentRequest(
                transaction.WalletTransactionsId,
                command.UserId,
                orderCode,
                transaction.TokenAmount,
                transaction.VndAmount,
                $"GigBridge token top-up {transaction.TokenAmount:0.####}",
                command.Request.ReturnUrl,
                command.Request.CancelUrl),
            cancellationToken);

        transaction.GatewayProvider = paymentResult.GatewayProvider;
        transaction.GatewayOrderCode = paymentResult.GatewayOrderCode;
        transaction.GatewayTransactionCode = paymentResult.GatewayTransactionCode;
        transaction.Metadata = paymentResult.CheckoutUrl;

        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(transaction);
    }

    private static long GenerateOrderCode(DateTime now)
    {
        var timestamp = long.Parse(now.ToString("MMddHHmmss"));
        var suffix = Random.Shared.Next(1000, 9999);
        return timestamp * 10000 + suffix;
    }

    private static CreateWalletTopUpResponse ToResponse(WalletTransaction transaction)
    {
        return new CreateWalletTopUpResponse(
            transaction.WalletTransactionsId,
            transaction.TokenAmount,
            transaction.VndAmount,
            transaction.GatewayProvider ?? "PayOS",
            transaction.GatewayOrderCode ?? string.Empty,
            transaction.GatewayTransactionCode,
            transaction.Metadata,
            transaction.Status);
    }
}
