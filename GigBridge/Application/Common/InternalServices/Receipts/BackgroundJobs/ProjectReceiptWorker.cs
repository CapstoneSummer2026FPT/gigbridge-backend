using System.Security.Cryptography;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Documents;
using Application.Common.Interfaces.Email;
using Application.Common.InternalServices.Receipts.Models;
using Application.Common.InternalServices.Receipts.Services;
using Application.Features.Auth.Shared.DTOs;
using Application.Common.InternalServices.Notifications.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Common.InternalServices.Receipts.BackgroundJobs;

public sealed class ProjectReceiptWorker : BackgroundService
{
    private const int MaxAttempts = 5;
    private const long WorkerLock = 0x5245434549505457;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(3);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectReceiptWorker> _logger;

    public ProjectReceiptWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectReceiptWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Project receipt worker batch failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task<bool> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var claim = await ClaimGenerationAsync(cancellationToken);
        if (claim is not null)
        {
            await GenerateAsync(claim, cancellationToken);
            var generatedDeliveryId = await ClaimDeliveryAsync(cancellationToken);
            if (generatedDeliveryId.HasValue)
            {
                await DeliverReadyAsync(generatedDeliveryId.Value, cancellationToken);
            }
            return true;
        }

        var deliveryId = await ClaimDeliveryAsync(cancellationToken);
        if (deliveryId.HasValue)
        {
            await DeliverReadyAsync(deliveryId.Value, cancellationToken);
            return true;
        }
        return false;
    }

    private async Task<GenerationClaim?> ClaimGenerationAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(WorkerLock, cancellationToken);
        var now = DateTime.UtcNow;
        var receipt = await context.Set<ProjectReceipt>()
            .Where(item =>
                (item.GenerationAttemptCount < MaxAttempts &&
                 ((item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Pending &&
                  item.NextGenerationAttemptAt <= now) ||
                 (item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Failed &&
                  item.NextGenerationAttemptAt <= now))) ||
                 (item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Processing &&
                  item.GenerationLeaseExpiresAt < now))
            .OrderBy(item => item.NextGenerationAttemptAt)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (receipt is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (receipt.GenerationStatus == (int)ProjectReceiptGenerationStatus.Processing &&
            receipt.GenerationAttemptCount >= MaxAttempts)
        {
            receipt.GenerationStatus = (int)ProjectReceiptGenerationStatus.Failed;
            receipt.GenerationLeaseToken = null;
            receipt.GenerationLeaseExpiresAt = null;
            receipt.GenerationLastError = "PDF generation lease expired after the maximum number of attempts.";
            receipt.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var token = Guid.NewGuid();
        receipt.GenerationStatus = (int)ProjectReceiptGenerationStatus.Processing;
        receipt.GenerationLeaseToken = token;
        receipt.GenerationLeaseExpiresAt = now.Add(LeaseDuration);
        receipt.GenerationAttemptCount++;
        receipt.GenerationLastError = null;
        receipt.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GenerationClaim(
            receipt.ProjectReceiptId,
            token,
            receipt.SnapshotJson,
            receipt.SnapshotHashSha256,
            receipt.GenerationAttemptCount);
    }

    private async Task GenerateAsync(GenerationClaim claim, CancellationToken cancellationToken)
    {
        try
        {
            using var generationScope = _scopeFactory.CreateScope();
            var generator = generationScope.ServiceProvider
                .GetRequiredService<IProjectReceiptDocumentGenerator>();
            var converter = generationScope.ServiceProvider.GetRequiredService<IWordToPdfConverter>();
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<ProjectReceiptSnapshot>(
                    claim.SnapshotJson,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException("The project receipt snapshot is invalid.");
            var document = generator.Generate(snapshot, claim.SnapshotHash);
            var pdf = await converter.ConvertAsync(
                document.Content,
                document.FileName,
                cancellationToken);

            using var persistenceScope = _scopeFactory.CreateScope();
            var context = persistenceScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var receipt = await context.Set<ProjectReceipt>()
                .FirstOrDefaultAsync(item =>
                    item.ProjectReceiptId == claim.ReceiptId &&
                    item.GenerationLeaseToken == claim.LeaseToken &&
                    item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Processing,
                    cancellationToken);
            if (receipt is null) return;
            var now = DateTime.UtcNow;
            receipt.PdfContent = pdf;
            receipt.PdfFileName = $"GigBridge-{receipt.ReceiptNumber}.pdf";
            receipt.PdfContentType = "application/pdf";
            receipt.PdfSizeBytes = pdf.LongLength;
            receipt.PdfHashSha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
            receipt.GeneratedAt = now;
            receipt.GenerationStatus = (int)ProjectReceiptGenerationStatus.Ready;
            receipt.GenerationLeaseToken = null;
            receipt.GenerationLeaseExpiresAt = null;
            receipt.GenerationLastError = null;
            receipt.NextEmailAttemptAt = now;
            receipt.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkGenerationFailedAsync(claim, exception, cancellationToken);
        }
    }

    private async Task MarkGenerationFailedAsync(
        GenerationClaim claim,
        Exception exception,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var receipt = await context.Set<ProjectReceipt>()
            .FirstOrDefaultAsync(item =>
                item.ProjectReceiptId == claim.ReceiptId &&
                item.GenerationLeaseToken == claim.LeaseToken,
                cancellationToken);
        if (receipt is null) return;
        var now = DateTime.UtcNow;
        receipt.GenerationStatus = (int)ProjectReceiptGenerationStatus.Failed;
        receipt.GenerationLeaseToken = null;
        receipt.GenerationLeaseExpiresAt = null;
        receipt.GenerationLastError = LimitError(exception.Message);
        receipt.NextGenerationAttemptAt = now.Add(RetryDelay(claim.AttemptCount));
        receipt.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        _logger.LogWarning(exception, "Receipt {ReceiptId} PDF generation failed.", claim.ReceiptId);
    }

    private async Task<Guid?> ClaimDeliveryAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(WorkerLock, cancellationToken);
        var now = DateTime.UtcNow;
        var receipt = await context.Set<ProjectReceipt>()
            .Where(item =>
                item.GenerationStatus == (int)ProjectReceiptGenerationStatus.Ready &&
                (item.DeliveryLeaseExpiresAt == null || item.DeliveryLeaseExpiresAt <= now) &&
                ((item.NotificationId == null &&
                  item.NotificationAttemptCount < MaxAttempts &&
                  item.NextNotificationAttemptAt <= now) ||
                 (item.EmailStatus != (int)ProjectReceiptEmailStatus.Delivered &&
                  item.EmailAttemptCount < MaxAttempts &&
                  item.NextEmailAttemptAt <= now)))
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (receipt is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        // The advisory lock serializes claims. The persisted lease prevents another
        // application instance from selecting the same receipt after this transaction
        // commits; a crashed worker becomes eligible again when the lease expires.
        receipt.DeliveryLeaseExpiresAt = now.Add(LeaseDuration);
        receipt.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt.ProjectReceiptId;
    }

    private async Task DeliverReadyAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureNotificationAsync(receiptId, cancellationToken);
            await SendEmailAsync(receiptId, cancellationToken);
        }
        finally
        {
            await ReleaseDeliveryLeaseAsync(receiptId, cancellationToken);
        }
    }

    private async Task EnsureNotificationAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var receipt = await context.Set<ProjectReceipt>()
            .Include(item => item.Contract)
            .FirstOrDefaultAsync(item => item.ProjectReceiptId == receiptId, cancellationToken);
        var now = DateTime.UtcNow;
        if (receipt is null || receipt.GenerationStatus != (int)ProjectReceiptGenerationStatus.Ready ||
            receipt.NotificationId.HasValue || receipt.NotificationAttemptCount >= MaxAttempts ||
            receipt.NextNotificationAttemptAt > now)
        {
            return;
        }

        var existing = await context.Set<Notification>()
            .FirstOrDefaultAsync(item =>
                item.UserId == receipt.OwnerUserId &&
                item.ReferenceId == receipt.ProjectReceiptId &&
                item.ReferenceType == nameof(ProjectReceipt),
                cancellationToken);
        if (existing is null)
        {
            receipt.NotificationAttemptCount++;
            await context.SaveChangesAsync(cancellationToken);
            try
            {
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notifications.CreateNotificationAsync(
                    receipt.OwnerUserId,
                    NotificationType.ReceiptReady,
                    "Project receipt ready",
                    $"Your receipt for {receipt.Contract.Title} is ready to download.",
                    receipt.ProjectReceiptId,
                    nameof(ProjectReceipt),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                receipt.NotificationLastError = LimitError(exception.Message);
                receipt.NextNotificationAttemptAt = DateTime.UtcNow.Add(
                    RetryDelay(receipt.NotificationAttemptCount));
                receipt.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(exception, "Receipt {ReceiptId} notification delivery failed.", receiptId);
                return;
            }
            existing = await context.Set<Notification>()
                .FirstOrDefaultAsync(item =>
                    item.UserId == receipt.OwnerUserId &&
                    item.ReferenceId == receipt.ProjectReceiptId &&
                    item.ReferenceType == nameof(ProjectReceipt),
                    cancellationToken);
        }
        if (existing is null)
        {
            receipt.NotificationLastError = "Notification service completed without creating a notification.";
            receipt.NextNotificationAttemptAt = DateTime.UtcNow.Add(
                RetryDelay(receipt.NotificationAttemptCount));
            receipt.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }
        receipt.NotificationId = existing.NotificationsId;
        receipt.NotifiedAt = existing.CreatedAt;
        receipt.NotificationLastError = null;
        receipt.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SendEmailAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var receipt = await context.Set<ProjectReceipt>()
            .Include(item => item.OwnerUser)
            .Include(item => item.Contract)
            .FirstOrDefaultAsync(item => item.ProjectReceiptId == receiptId, cancellationToken);
        if (receipt is null || receipt.GenerationStatus != (int)ProjectReceiptGenerationStatus.Ready ||
            receipt.EmailStatus == (int)ProjectReceiptEmailStatus.Delivered ||
            receipt.EmailAttemptCount >= MaxAttempts ||
            receipt.NextEmailAttemptAt > DateTime.UtcNow ||
            receipt.PdfContent is not { Length: > 0 } || string.IsNullOrWhiteSpace(receipt.PdfFileName))
        {
            return;
        }

        receipt.EmailAttemptCount++;
        await context.SaveChangesAsync(cancellationToken);
        try
        {
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var roleLabel = receipt.ReceiptType == (int)ProjectReceiptType.Client
                ? "transaction receipt"
                : "payment receipt";
            await email.SendEmailAsync(new EmailRequest
            {
                To = receipt.OwnerUser.Email,
                Subject = $"[GigBridge] Your {roleLabel} is ready",
                Body = $"<p>Hello {System.Net.WebUtility.HtmlEncode(receipt.OwnerUser.FullName)},</p>" +
                    $"<p>Your {roleLabel} for <strong>{System.Net.WebUtility.HtmlEncode(receipt.Contract.Title)}</strong> is attached.</p>",
                TextBody = $"Hello {receipt.OwnerUser.FullName}, your {roleLabel} for {receipt.Contract.Title} is attached.",
                IsHtml = true,
                IdempotencyKey = $"project-receipt-{receipt.ProjectReceiptId:N}",
                MessageId = $"<project-receipt-{receipt.ProjectReceiptId:N}@gigbridge.local>",
                ByteAttachments =
                [
                    new EmailByteAttachment(
                        receipt.PdfFileName,
                        receipt.PdfContent,
                        receipt.PdfContentType ?? "application/pdf")
                ]
            }, cancellationToken);
            receipt.EmailStatus = (int)ProjectReceiptEmailStatus.Delivered;
            receipt.EmailedAt = DateTime.UtcNow;
            receipt.EmailLastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            receipt.EmailStatus = (int)ProjectReceiptEmailStatus.Failed;
            receipt.EmailLastError = LimitError(exception.Message);
            receipt.NextEmailAttemptAt = DateTime.UtcNow.Add(RetryDelay(receipt.EmailAttemptCount));
            _logger.LogWarning(exception, "Receipt {ReceiptId} email delivery failed.", receiptId);
        }
        receipt.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseDeliveryLeaseAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var receipt = await context.Set<ProjectReceipt>()
            .FirstOrDefaultAsync(item => item.ProjectReceiptId == receiptId, cancellationToken);
        if (receipt is null) return;
        receipt.DeliveryLeaseExpiresAt = null;
        receipt.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan RetryDelay(int attempt) => attempt switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromHours(6)
    };

    private static string LimitError(string value) =>
        value.Length <= 2000 ? value : value[..2000];

    private sealed record GenerationClaim(
        Guid ReceiptId,
        Guid LeaseToken,
        string SnapshotJson,
        string SnapshotHash,
        int AttemptCount);
}
