using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.ProductHandoffs.Acknowledge.Commands;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Application.Features.Contracts.ProductHandoffs.Download.Queries;
using Application.Features.Contracts.ProductHandoffs.GetList.Queries;
using Application.Features.Contracts.ProductHandoffs.Submit.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class ContractProductHandoffWorkflowTests
{
    [Fact]
    public async Task SubmitFileHandoff_UploadsToContractProductsAndCreatesCurrentVersion()
    {
        var fixture = new ContractProductHandoffFixture();
        var handler = fixture.CreateSubmitHandler();
        var file = new SubmitContractProductHandoffFile(
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "brief.pdf",
            "application/pdf",
            3);

        var response = await handler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                file,
                null,
                "Please work from this brief."),
            CancellationToken.None);

        Assert.Equal(1, response.Version);
        Assert.True(response.IsCurrent);
        Assert.Equal((int)ContractProductHandoffSourceType.File, response.SourceType);
        Assert.Equal("https://res.cloudinary.com/gigbridge/product.pdf", response.FileUrl);
        Assert.Equal("contract-products", fixture.MediaService.Uploads[0].Folder);
        Assert.Equal("brief.pdf", fixture.MediaService.Uploads[0].FileName);
        Assert.Single(fixture.Handoffs.Entities);
        Assert.Single(fixture.Context.Set<Message>().ToList());
    }

    [Fact]
    public async Task SubmitFileHandoff_PushesNamedMessageLiveToTheWorkspaceConversation()
    {
        var fixture = new ContractProductHandoffFixture();
        var realtime = new CapturingChatRealtimeNotifier();
        var handler = fixture.CreateSubmitHandler(realtime);
        var file = new SubmitContractProductHandoffFile(
            new MemoryStream(new byte[] { 1, 2, 3 }),
            "brief.pdf",
            "application/pdf",
            3);

        await handler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                file,
                null,
                "Please work from this brief."),
            CancellationToken.None);

        var message = Assert.Single(fixture.Context.Set<Message>().ToList());
        Assert.Contains("brief.pdf", message.Content);

        var conversationEvent = Assert.Single(realtime.ConversationEvents);
        Assert.Equal("ReceiveMessage", conversationEvent.EventName);
        Assert.Equal(message.ConversationsId, conversationEvent.ConversationId);
    }

    [Fact]
    public async Task SubmitLinkHandoff_DoesNotUploadFile()
    {
        var fixture = new ContractProductHandoffFixture();
        var handler = fixture.CreateSubmitHandler();

        var response = await handler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/product.zip",
                null),
            CancellationToken.None);

        Assert.Equal((int)ContractProductHandoffSourceType.Link, response.SourceType);
        Assert.Equal("https://example.com/product.zip", response.ExternalUrl);
        Assert.Empty(fixture.MediaService.Uploads);
    }

    [Fact]
    public async Task SubmitHandoff_RequiresExactlyOneSourceAndValidFile()
    {
        var fixture = new ContractProductHandoffFixture();
        var handler = fixture.CreateSubmitHandler();
        var validFile = new SubmitContractProductHandoffFile(
            new MemoryStream(new byte[] { 1 }),
            "brief.pdf",
            "application/pdf",
            1);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SubmitContractProductHandoffCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    null,
                    null,
                    null),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SubmitContractProductHandoffCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    validFile,
                    "https://example.com/product.zip",
                    null),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SubmitContractProductHandoffCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SubmitContractProductHandoffFile(
                        new MemoryStream(new byte[] { 1 }),
                        "script.exe",
                        "application/octet-stream",
                        1),
                    null,
                    null),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SubmitContractProductHandoffCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SubmitContractProductHandoffFile(
                        new MemoryStream(new byte[] { 1 }),
                        "huge.zip",
                        "application/zip",
                        100 * 1024 * 1024 + 1),
                    null,
                    null),
                CancellationToken.None));
    }

    [Fact]
    public async Task SubmitSecondHandoff_PreservesHistoryAndMovesCurrentVersion()
    {
        var fixture = new ContractProductHandoffFixture();
        var handler = fixture.CreateSubmitHandler();

        await handler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/v1",
                null),
            CancellationToken.None);

        var second = await handler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/v2",
                null),
            CancellationToken.None);

        Assert.Equal(2, second.Version);
        Assert.Equal(2, fixture.Handoffs.Entities.Count);
        Assert.False(fixture.Handoffs.Entities.Single(handoff => handoff.Version == 1).IsCurrent);
        Assert.True(fixture.Handoffs.Entities.Single(handoff => handoff.Version == 2).IsCurrent);
    }

    [Fact]
    public async Task SubmitHandoff_RejectsWrongUserAndInactiveContract()
    {
        var fixture = new ContractProductHandoffFixture();
        var handler = fixture.CreateSubmitHandler();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new SubmitContractProductHandoffCommand(
                    fixture.ContractId,
                    fixture.FreelancerUserId,
                    null,
                    "https://example.com/product",
                    null),
                CancellationToken.None));

        fixture.Contract.Status = (int)ContractStatus.PendingEscrow;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SubmitContractProductHandoffCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    null,
                    "https://example.com/product",
                    null),
                CancellationToken.None));
    }

    [Fact]
    public async Task AcknowledgeHandoff_AllowsSelectedFreelancerAndIsIdempotent()
    {
        var fixture = new ContractProductHandoffFixture();
        var submitHandler = fixture.CreateSubmitHandler();
        var handoff = await submitHandler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/product",
                null),
            CancellationToken.None);

        var acknowledgeHandler = fixture.CreateAcknowledgeHandler();

        var acknowledged = await acknowledgeHandler.Handle(
            new AcknowledgeContractProductHandoffCommand(
                fixture.ContractId,
                handoff.ContractProductHandoffId,
                fixture.FreelancerUserId),
            CancellationToken.None);

        var acknowledgedAgain = await acknowledgeHandler.Handle(
            new AcknowledgeContractProductHandoffCommand(
                fixture.ContractId,
                handoff.ContractProductHandoffId,
                fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(fixture.FreelancerUserId, acknowledged.ReceivedByUserId);
        Assert.NotNull(acknowledged.ReceivedAt);
        Assert.Equal(acknowledged.ReceivedAt, acknowledgedAgain.ReceivedAt);
    }

    [Fact]
    public async Task AcknowledgeHandoff_RejectsClientAndOutsider()
    {
        var fixture = new ContractProductHandoffFixture();
        var submitHandler = fixture.CreateSubmitHandler();
        var handoff = await submitHandler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/product",
                null),
            CancellationToken.None);
        var acknowledgeHandler = fixture.CreateAcknowledgeHandler();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            acknowledgeHandler.Handle(
                new AcknowledgeContractProductHandoffCommand(
                    fixture.ContractId,
                    handoff.ContractProductHandoffId,
                    fixture.ClientUserId),
                CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            acknowledgeHandler.Handle(
                new AcknowledgeContractProductHandoffCommand(
                    fixture.ContractId,
                    handoff.ContractProductHandoffId,
                    fixture.OutsiderUserId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Queries_ReturnHistoryAndDownloadForParticipantsOnly()
    {
        var fixture = new ContractProductHandoffFixture();
        var submitHandler = fixture.CreateSubmitHandler();
        var first = await submitHandler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/v1",
                null),
            CancellationToken.None);
        var second = await submitHandler.Handle(
            new SubmitContractProductHandoffCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                null,
                "https://example.com/v2",
                null),
            CancellationToken.None);

        var listHandler = new GetContractProductHandoffsQueryHandler(fixture.Context);
        var downloadHandler = new GetContractProductHandoffDownloadQueryHandler(fixture.Context);

        var list = await listHandler.Handle(
            new GetContractProductHandoffsQuery(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        var download = await downloadHandler.Handle(
            new GetContractProductHandoffDownloadQuery(
                fixture.ContractId,
                second.ContractProductHandoffId,
                fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal([second.ContractProductHandoffId, first.ContractProductHandoffId], list.Select(x => x.ContractProductHandoffId));
        Assert.Equal("https://example.com/v2", download.Url);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            listHandler.Handle(
                new GetContractProductHandoffsQuery(fixture.ContractId, fixture.OutsiderUserId),
                CancellationToken.None));
    }

    private sealed class ContractProductHandoffFixture
    {
        public ContractProductHandoffFixture()
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = Guid.NewGuid(),
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Active contract",
                TotalBudget = 1_000_000m,
                Status = (int)ContractStatus.Active,
                CreatedAt = Now
            };

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" },
                new User { UserId = OutsiderUserId, Role = (int)UserRole.Client, Email = "outsider@example.com", FullName = "Outsider" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(Contract);
            Context.AddSet(new Conversation
            {
                ConversationsId = Guid.NewGuid(),
                ConversationType = (int)ConversationType.ContractWorkroom,
                ContractsId = ContractId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            });
            Context.AddSet<Message>();
            Handoffs = Context.AddSet<ContractProductHandoff>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 30, 3, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid OutsiderUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public TestDbSet<ContractProductHandoff> Handoffs { get; }
        public FakeMediaService MediaService { get; } = new("https://res.cloudinary.com/gigbridge/product.pdf");

        public SubmitContractProductHandoffCommandHandler CreateSubmitHandler(
            IChatRealtimeNotifier? realtimeNotifier = null)
        {
            return new SubmitContractProductHandoffCommandHandler(
                Context,
                new FixedDateTimeService(Now),
                MediaService,
                new NoopNotificationService(),
                realtimeNotifier ?? new NoopChatRealtimeNotifier());
        }

        public AcknowledgeContractProductHandoffCommandHandler CreateAcknowledgeHandler()
        {
            return new AcknowledgeContractProductHandoffCommandHandler(
                Context,
                new FixedDateTimeService(Now.AddMinutes(1)),
                new NoopChatRealtimeNotifier());
        }
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
