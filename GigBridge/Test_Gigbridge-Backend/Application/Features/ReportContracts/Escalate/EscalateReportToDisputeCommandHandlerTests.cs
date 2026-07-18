using Application.Common.Interfaces.IService;
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
            Status = (int)ContractReportStatus.WaitingReporterConfirmation,
            CreatedAt = now
        };

        var context = new InMemoryApplicationDbContext();
        context.AddSet(contract);
        context.AddSet(report);
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
        var messageCountsAtSave = new List<int>();
        context.OnSaveChanges = _ => messageCountsAtSave.Add(messages.Entities.Count);

        var realtimeSaveCounts = new List<int>();
        var realtimeNotifier = Substitute.For<IChatRealtimeNotifier>();
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
                report.Description,
                null,
                report.DesiredResolution),
            CancellationToken.None);

        var dispute = Assert.Single(disputes.Entities);
        var conversation = Assert.Single(conversations.Entities);
        var systemMessage = Assert.Single(messages.Entities);

        Assert.Equal(dispute.DisputesId, result.DisputesId);
        Assert.Equal(dispute.DisputesId, conversation.DisputesId);
        Assert.Equal((int)ConversationType.Dispute, conversation.ConversationType);
        Assert.Equal(2, conversationParticipants.Entities.Count);
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
        Assert.Equal(new[] { 2 }, realtimeSaveCounts);

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
