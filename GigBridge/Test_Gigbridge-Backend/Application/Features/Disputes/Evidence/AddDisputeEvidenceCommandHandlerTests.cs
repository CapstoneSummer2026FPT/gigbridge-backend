using Application.Common.Exceptions;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Features.Disputes.Common.Internal;
using Application.Features.Disputes.Evidence.Add.Commands;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Disputes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Disputes.Evidence;

public sealed class AddDisputeEvidenceCommandHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AllowsEitherContractPartyAndPersistsEvidence(bool uploadAsClient)
    {
        var fixture = CreateFixture((int)DisputeStatus.WaitingAdmin);
        var uploaderId = uploadAsClient ? fixture.ClientUserId : fixture.FreelancerUserId;

        var result = await fixture.Handler.Handle(
            new AddDisputeEvidenceCommand(
                fixture.ContractId,
                fixture.DisputeId,
                uploaderId,
                [new DisputeEvidenceFile(
                    new MemoryStream([1, 2, 3]),
                    "proof.pdf",
                    "application/pdf",
                    3)]),
            CancellationToken.None);

        var evidence = Assert.Single(fixture.Evidences.Entities);
        Assert.Equal(evidence.DisputeEvidenceId, Assert.Single(result).DisputeEvidenceId);
        Assert.Equal(uploaderId, evidence.UploadedById);
        Assert.Equal(fixture.DisputeId, evidence.DisputesId);
        Assert.Equal("https://files.example/proof.pdf", evidence.FileUrl);
        Assert.Equal(1, fixture.Context.SaveChangesCount);
    }

    [Theory]
    [InlineData((int)DisputeStatus.Resolved)]
    [InlineData((int)DisputeStatus.Closed)]
    public async Task Handle_RejectsEvidenceAfterActiveDisputeStatuses(int status)
    {
        var fixture = CreateFixture(status);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Handler.Handle(
            new AddDisputeEvidenceCommand(
                fixture.ContractId,
                fixture.DisputeId,
                fixture.ClientUserId,
                [new DisputeEvidenceFile(
                    new MemoryStream([1]),
                    "proof.pdf",
                    "application/pdf",
                    1)]),
            CancellationToken.None));

        Assert.Equal("Evidence can only be added while the dispute is active.", exception.Message);
        Assert.Empty(fixture.Evidences.Entities);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    private static Fixture CreateFixture(int disputeStatus)
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
        var contractId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();

        context.AddSet(new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            Title = "Evidence contract",
            Status = (int)ContractStatus.Disputed,
            CreatedAt = now
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
        context.AddSet(new Dispute
        {
            DisputesId = disputeId,
            ContractsId = contractId,
            InitiatorId = clientUserId,
            RespondentId = freelancerUserId,
            Reason = "Evidence test",
            Status = disputeStatus,
            Urgency = (int)DisputeUrgency.Normal,
            CreatedAt = now
        });
        var evidences = context.AddSet<DisputeEvidence>();

        var mediaService = Substitute.For<IMediaService>();
        mediaService.UploadFileAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("https://files.example/proof.pdf");

        var handler = new AddDisputeEvidenceCommandHandler(
            context,
            new FixedDateTimeService(now),
            mediaService,
            new NoopNotificationService(),
            Substitute.For<ILogger<AddDisputeEvidenceCommandHandler>>());

        return new Fixture(
            context,
            handler,
            evidences,
            contractId,
            disputeId,
            clientUserId,
            freelancerUserId);
    }

    private sealed record Fixture(
        InMemoryApplicationDbContext Context,
        AddDisputeEvidenceCommandHandler Handler,
        TestDbSet<DisputeEvidence> Evidences,
        Guid ContractId,
        Guid DisputeId,
        Guid ClientUserId,
        Guid FreelancerUserId);

    private sealed class FixedDateTimeService(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }
}
