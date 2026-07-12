using System.Security.Cryptography;
using System.Text;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Webhook;

public sealed class HandlePayoutWebhookCommandHandler :
    IRequestHandler<HandlePayoutWebhookCommand, PayoutWebhookResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IPayoutProvider _payoutProvider;

    public HandlePayoutWebhookCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IPayoutProvider payoutProvider)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _payoutProvider = payoutProvider;
    }

    public async Task<PayoutWebhookResponse> Handle(
        HandlePayoutWebhookCommand command,
        CancellationToken cancellationToken)
    {
        var verification = await _payoutProvider.VerifyWebhookAsync(
            new PayoutWebhookVerificationRequest(command.RawPayload, command.Signature),
            cancellationToken);

        if (!verification.IsVerified)
        {
            var rejected = await AddWebhookLogAsync(
                verification,
                null,
                PayoutWebhookProcessingStatus.Rejected,
                "Payout webhook signature is invalid.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("Payout webhook signature is invalid.");
        }

        var signatureHash = HashSignature(verification.EventId, verification.RawPayload);
        if (!string.IsNullOrWhiteSpace(verification.EventId) ||
            !string.IsNullOrWhiteSpace(signatureHash))
        {
            var existing = await _context.Set<PayoutWebhookLog>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    log =>
                        log.Provider == _payoutProvider.ProviderName &&
                        ((!string.IsNullOrWhiteSpace(verification.EventId) && log.EventId == verification.EventId) ||
                            (!string.IsNullOrWhiteSpace(signatureHash) && log.SignatureHash == signatureHash)) &&
                        log.ProcessingStatus == (int)PayoutWebhookProcessingStatus.Processed,
                    cancellationToken);

            if (existing is not null)
            {
                return new PayoutWebhookResponse(
                    existing.PayoutWebhookLogId,
                    existing.WalletWithdrawalId,
                    existing.ProcessingStatus);
            }
        }

        var withdrawal = await ResolveWithdrawalAsync(verification, cancellationToken);
        if (withdrawal is null)
        {
            var failed = await AddWebhookLogAsync(
                verification,
                null,
                PayoutWebhookProcessingStatus.Failed,
                "Withdrawal was not found for provider webhook.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return new PayoutWebhookResponse(
                failed.PayoutWebhookLogId,
                null,
                failed.ProcessingStatus);
        }

        var log = await AddWebhookLogAsync(
            verification,
            withdrawal.WalletWithdrawalId,
            PayoutWebhookProcessingStatus.Pending,
            null,
            cancellationToken);

        await WithdrawalWorkflow.ApplyWebhookResultAsync(
            _context,
            _dateTimeService,
            withdrawal,
            verification,
            cancellationToken);

        log.ProcessingStatus = (int)PayoutWebhookProcessingStatus.Processed;
        log.ProcessedAt = _dateTimeService.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new PayoutWebhookResponse(
            log.PayoutWebhookLogId,
            withdrawal.WalletWithdrawalId,
            log.ProcessingStatus);
    }

    private async Task<WalletWithdrawal?> ResolveWithdrawalAsync(
        PayoutWebhookVerificationResult verification,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(verification.ProviderOrderCode))
        {
            var byOrderCode = await _context.Set<WalletWithdrawal>()
                .FirstOrDefaultAsync(
                    withdrawal =>
                        withdrawal.Provider == _payoutProvider.ProviderName &&
                        withdrawal.ProviderOrderCode == verification.ProviderOrderCode,
                    cancellationToken);

            if (byOrderCode is not null)
            {
                return byOrderCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(verification.ProviderPayoutId))
        {
            return await _context.Set<WalletWithdrawal>()
                .FirstOrDefaultAsync(
                    withdrawal =>
                        withdrawal.Provider == _payoutProvider.ProviderName &&
                        withdrawal.ProviderPayoutId == verification.ProviderPayoutId,
                    cancellationToken);
        }

        return null;
    }

    private Task<PayoutWebhookLog> AddWebhookLogAsync(
        PayoutWebhookVerificationResult verification,
        Guid? withdrawalId,
        PayoutWebhookProcessingStatus status,
        string? error,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var log = new PayoutWebhookLog
        {
            PayoutWebhookLogId = Guid.NewGuid(),
            Provider = _payoutProvider.ProviderName,
            EventId = verification.EventId,
            SignatureHash = HashSignature(verification.EventId, verification.RawPayload),
            WalletWithdrawalId = withdrawalId,
            RawPayload = verification.RawPayload ?? string.Empty,
            ProcessingStatus = (int)status,
            Error = error,
            ReceivedAt = _dateTimeService.UtcNow,
            ProcessedAt = status == PayoutWebhookProcessingStatus.Pending ? null : _dateTimeService.UtcNow
        };

        _context.Set<PayoutWebhookLog>().Add(log);
        return Task.FromResult(log);
    }

    private static string HashSignature(string? eventId, string? rawPayload)
    {
        using var sha = SHA256.Create();
        var value = $"{eventId ?? string.Empty}:{rawPayload ?? string.Empty}";
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
