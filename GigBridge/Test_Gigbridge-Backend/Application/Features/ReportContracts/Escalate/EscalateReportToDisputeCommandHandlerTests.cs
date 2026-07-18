using Application.Common.Interfaces.IService;
using Application.Features.Disputes.Common.Internal;
using Application.Features.ReportContracts.Escalate.Commands;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.ReportContracts.Escalate;

public sealed class EscalateReportToDisputeCommandHandlerTests
{
    [Fact]
    public async Task Validator_RequiresDeclarationAndUrgency()
    {
        var validator = new EscalateReportToDisputeCommandValidator();
        var result = await validator.ValidateAsync(new EscalateReportToDisputeCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dispute",
            "Description",
            0,
            "Requested resolution",
            null,
            false,
            []));

        Assert.Contains(result.Errors, error => error.PropertyName == "Urgency");
        Assert.Contains(result.Errors, error => error.PropertyName == "DeclarationAccepted");
    }

    [Fact]
    public async Task Handle_RejectsZeroClaimForPaymentReportBeforeCreatingAnything()
    {
        var now = new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc);
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();

        context.AddSet(new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            Title = "Payment contract",
            Status = (int)ContractStatus.Active,
            CreatedAt = now
        });
        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId, CreatedAt = now });
        context.AddSet(new FreelancerProfile { FreelancerProfilesId = freelancerProfileId, UserId = freelancerUserId, CreatedAt = now });
        context.AddSet(new ReportContract
        {
            ReportContractId = reportId,
            ContractId = contractId,
            ReporterId = clientUserId,
            RespondentId = freelancerUserId,
            IssueType = (int)ContractReportIssueType.PaymentIssue,
            Description = "Payment has not been released.",
            DesiredResolution = "Release payment.",
            Status = (int)ContractReportStatus.WaitingReporterConfirmation,
            CreatedAt = now
        });
        var disputes = context.AddSet<Dispute>();
        var conversations = context.AddSet<Conversation>();

        var handler = new EscalateReportToDisputeCommandHandler(
            context,
            new FixedDateTimeService(now),
            Substitute.For<IMediaService>(),
            new NoopNotificationService(),
            Substitute.For<IChatRealtimeNotifier>(),
            Substitute.For<ILogger<EscalateReportToDisputeCommandHandler>>());

        var exception = await Assert.ThrowsAsync<global::Application.Common.Exceptions.BadRequestException>(() => handler.Handle(
            new EscalateReportToDisputeCommand(
                contractId,
                reportId,
                clientUserId,
                "Payment dispute",
                "Payment has not been released.",
                0,
                "Release payment.",
                DisputeUrgency.Normal,
                true,
                []),
            CancellationToken.None));

        Assert.Equal("Claimed amount must be greater than 0 for payment or milestone disputes.", exception.Message);
        Assert.Empty(disputes.Entities);
        Assert.Empty(conversations.Entities);
        Assert.Equal(0, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_PersistsConversationBeforeInitialSystemMessage()
    {
        var now = new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc);
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var contract = new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            Title = "Escalated contract",
            Status = (int)ContractStatus.Active,
            CreatedAt = now
        };
        var report = new ReportContract
        {
            ReportContractId = reportId,
            ContractId = contractId,
            ReporterId = clientUserId,
            RespondentId = freelancerUserId,
            Description = "The proposed resolution was not accepted.",
            DesiredResolution = "Resolve the contract issue.",
            IssueType = (int)ContractReportIssueType.PaymentIssue,
            Status = (int)ContractReportStatus.WaitingReporterConfirmation,
            CreatedAt = now
        };

        var context = new InMemoryApplicationDbContext();
        context.AddSet(contract);
        context.AddSet(report);
        context.AddSet(new ReportContractAttachment
        {
            ReportContractAttachmentId = Guid.NewGuid(),
            ReportContractId = reportId,
            FileName = "original-report.pdf",
            FileUrl = "https://files.example/original-report.pdf",
            ContentType = "application/pdf",
            FileSize = 1200,
            UploadedAt = now.AddHours(-1),
            UploadedByUserId = clientUserId
        });
        context.AddSet(new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = clientUserId,
            CreatedAt = now
        });
        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancerUserId,
            CreatedAt = now
        });
        context.AddSet(new User
        {
            UserId = clientUserId,
            FullName = "Client Reporter",
            Email = "client@example.com",
            Role = (int)UserRole.Client,
            IsActive = true,
            CreatedAt = now
        });

        var disputes = context.AddSet<Dispute>();
        var conversations = context.AddSet<Conversation>();
        var conversationParticipants = context.AddSet<ConversationParticipant>();
        var messages = context.AddSet<Message>();
        var evidences = context.AddSet<DisputeEvidence>();
        var messageCountsAtSave = new List<int>();
        context.OnSaveChanges = _ => messageCountsAtSave.Add(messages.Entities.Count);

        var realtimeSaveCounts = new List<int>();
        var realtimeNotifier = Substitute.For<IChatRealtimeNotifier>();
        var mediaService = Substitute.For<IMediaService>();
        mediaService.UploadFileAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("https://files.example/additional.png");
        realtimeNotifier
            .When(notifier => notifier.SendConversationEventAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => realtimeSaveCounts.Add(context.SaveChangesCount));

        var handler = new EscalateReportToDisputeCommandHandler(
            context,
            new FixedDateTimeService(now),
            mediaService,
            new NoopNotificationService(),
            realtimeNotifier,
            Substitute.For<ILogger<EscalateReportToDisputeCommandHandler>>());

        var result = await handler.Handle(
            new EscalateReportToDisputeCommand(
                contractId,
                reportId,
                clientUserId,
                "Contract dispute",
                report.Description,
                250m,
                report.DesiredResolution,
                DisputeUrgency.High,
                true,
                [new DisputeEvidenceFile(
                    new MemoryStream([1, 2, 3]),
                    "additional.png",
                    "image/png",
                    3)]),
            CancellationToken.None);

        var dispute = Assert.Single(disputes.Entities);
        var conversation = Assert.Single(conversations.Entities);
        var systemMessage = Assert.Single(messages.Entities);

        Assert.Equal(dispute.DisputesId, result.DisputesId);
        Assert.Equal(dispute.DisputesId, conversation.DisputesId);
        Assert.Equal((int)ConversationType.Dispute, conversation.ConversationType);
        Assert.Equal(2, conversationParticipants.Entities.Count);
        Assert.Equal(2, evidences.Entities.Count);
        Assert.Contains(evidences.Entities, item =>
            item.FileName == "original-report.pdf" &&
            item.FileUrl == "https://files.example/original-report.pdf" &&
            item.CreatedAt == now.AddHours(-1));
        Assert.Contains(evidences.Entities, item =>
            item.FileName == "additional.png" &&
            item.FileUrl == "https://files.example/additional.png" &&
            item.CreatedAt == now);
        Assert.Contains(conversationParticipants.Entities, item =>
            item.UserId == clientUserId && item.ParticipantRole == (int)ParticipantRole.Client);
        Assert.Contains(conversationParticipants.Entities, item =>
            item.UserId == freelancerUserId && item.ParticipantRole == (int)ParticipantRole.Freelancer);

        Assert.Equal(new[] { 0, 1 }, messageCountsAtSave);
        Assert.Equal(2, context.SaveChangesCount);
        Assert.Equal(1, context.TransactionBeginCount);
        Assert.Equal(1, context.TransactionCommitCount);
        Assert.Equal(systemMessage.MessagesId, conversation.LastMessageId);
        Assert.Equal(conversation.ConversationsId, systemMessage.ConversationsId);
        Assert.Equal("A dispute has been opened.", systemMessage.Content);
        Assert.Equal((int)ContractReportStatus.Escalated, report.Status);
        Assert.True(report.IsEscalatedToDispute);
        Assert.Equal((int)ContractStatus.Disputed, contract.Status);
        Assert.Equal((int)DisputeUrgency.High, dispute.Urgency);
        Assert.Equal((int)ContractReportIssueType.PaymentIssue, result.IssueType);
        Assert.Equal(new[] { 2 }, realtimeSaveCounts);

        await mediaService.Received(1).UploadFileAsync(
            Arg.Any<Stream>(),
            "additional.png",
            "image/png",
            "disputes",
            Arg.Any<CancellationToken>());

        await realtimeNotifier.Received(1).SendConversationEventAsync(
            conversation.ConversationsId,
            "ReceiveMessage",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    private sealed class FixedDateTimeService(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }
}
