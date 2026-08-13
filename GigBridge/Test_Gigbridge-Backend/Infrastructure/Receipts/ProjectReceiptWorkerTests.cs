using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Documents;
using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Receipts.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Infrastructure.Receipts;

public sealed class ProjectReceiptWorkerTests
{
    [Fact]
    public async Task ProcessOnce_GeneratesOnceAndRetriesEmailWithoutRegeneratingPdf()
    {
        var now = DateTime.UtcNow;
        var owner = new User
        {
            UserId = Guid.NewGuid(),
            Role = (int)UserRole.Client,
            FullName = "Receipt Owner",
            Email = "owner@example.com"
        };
        var contract = new Contract
        {
            ContractsId = Guid.NewGuid(),
            Title = "Receipt project",
            Status = (int)ContractStatus.Completed,
            CompletedAt = now,
            CreatedAt = now
        };
        var snapshot = CreateSnapshot(owner, contract, now);
        var receipt = new ProjectReceipt
        {
            ProjectReceiptId = snapshot.ReceiptId,
            ContractsId = contract.ContractsId,
            OwnerUserId = owner.UserId,
            ReceiptType = (int)ProjectReceiptType.Client,
            ReceiptNumber = snapshot.ReceiptNumber,
            IssuedAt = now,
            SnapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            SnapshotHashSha256 = new string('a', 64),
            GenerationStatus = (int)ProjectReceiptGenerationStatus.Pending,
            NextGenerationAttemptAt = now.AddMinutes(-1),
            EmailStatus = (int)ProjectReceiptEmailStatus.Pending,
            NextEmailAttemptAt = now.AddMinutes(-1),
            CreatedAt = now,
            Contract = contract,
            OwnerUser = owner
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(receipt);
        var notifications = context.AddSet<Notification>();
        var generator = Substitute.For<IProjectReceiptDocumentGenerator>();
        generator.Generate(Arg.Any<ProjectReceiptSnapshot>(), receipt.SnapshotHashSha256)
            .Returns(new GeneratedProjectReceiptDocument([1, 2, 3], "receipt.docx", "application/docx"));
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 1, 2, 3 };
        var converter = Substitute.For<IWordToPdfConverter>();
        converter.ConvertAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(pdf);
        var notificationService = new PersistingNotificationService(notifications, now);
        var emailService = new FailFirstEmailService();
        var services = new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(context)
            .AddSingleton(generator)
            .AddSingleton(converter)
            .AddSingleton<INotificationService>(notificationService)
            .AddSingleton<IEmailService>(emailService)
            .BuildServiceProvider();
        var worker = new ProjectReceiptWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProjectReceiptWorker>.Instance);

        Assert.True(await worker.ProcessOnceAsync(CancellationToken.None));

        Assert.Equal((int)ProjectReceiptGenerationStatus.Ready, receipt.GenerationStatus);
        Assert.Equal((int)ProjectReceiptEmailStatus.Failed, receipt.EmailStatus);
        Assert.Equal(pdf, receipt.PdfContent);
        Assert.NotNull(receipt.NotificationId);
        Assert.Single(notifications.Entities);
        Assert.Equal(1, receipt.GenerationAttemptCount);
        receipt.NextEmailAttemptAt = DateTime.UtcNow.AddMinutes(-1);

        Assert.True(await worker.ProcessOnceAsync(CancellationToken.None));

        Assert.Equal((int)ProjectReceiptEmailStatus.Delivered, receipt.EmailStatus);
        Assert.Equal(2, receipt.EmailAttemptCount);
        Assert.Equal(1, receipt.GenerationAttemptCount);
        Assert.Single(notifications.Entities);
        Assert.Equal(2, emailService.Attempts);
        Assert.Equal($"project-receipt-{receipt.ProjectReceiptId:N}", emailService.LastRequest!.IdempotencyKey);
        Assert.Equal($"<project-receipt-{receipt.ProjectReceiptId:N}@gigbridge.local>", emailService.LastRequest.MessageId);
        var attachment = Assert.Single(emailService.LastRequest.ByteAttachments!);
        Assert.Equal(pdf, attachment.Content);
        await converter.Received(1).ConvertAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static ProjectReceiptSnapshot CreateSnapshot(User owner, Contract contract, DateTime now) => new(
        Guid.NewGuid(),
        "GB-RC-WORKER-TEST",
        (int)ProjectReceiptType.Client,
        now,
        "Completed",
        new ProjectReceiptPartySnapshot(owner.UserId, owner.FullName, "Cá nhân", owner.Email, "001234567890"),
        new ProjectReceiptPartySnapshot(Guid.NewGuid(), "Freelancer", "Cá nhân", "freelancer@example.com", "123456789"),
        "GB-CTR-WORKER",
        contract.ContractsId,
        contract.Title,
        null,
        null,
        now,
        100m,
        0m,
        100m,
        0m,
        100m,
        100m,
        0m,
        1m,
        100m,
        0m,
        99m,
        99m,
        "ESCROW-FINAL-WORKER",
        1_000m,
        [new ProjectReceiptMilestoneSnapshot(1, Guid.NewGuid(), "Delivery", now, 100m, 0m, 100m, 100m, 1m, 99m)]);

    private sealed class PersistingNotificationService(
        TestDbSet<Notification> notifications,
        DateTime now) : INotificationService
    {
        public Task CreateNotificationAsync(
            Guid userId,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            CancellationToken cancellationToken = default)
        {
            notifications.Add(new Notification
            {
                NotificationsId = Guid.NewGuid(),
                UserId = userId,
                Type = (int)type,
                Title = title,
                Content = content,
                ReferenceId = referenceId,
                ReferenceType = referenceType,
                CreatedAt = now
            });
            return Task.CompletedTask;
        }

        public Task CreateBroadcastNotificationAsync(
            NotificationTarget target,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            Guid? targetUserId = null,
            bool sendEmail = false,
            Guid? createdByAdminId = null,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FailFirstEmailService : IEmailService
    {
        public int Attempts { get; private set; }
        public EmailRequest? LastRequest { get; private set; }

        public Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
        {
            Attempts++;
            LastRequest = emailRequestDTO;
            return Attempts == 1
                ? Task.FromException(new InvalidOperationException("Temporary email failure"))
                : Task.CompletedTask;
        }
    }
}
