using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.ReportContracts.Create.Commands;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.ReportContracts.Create;

public sealed class CreateReportCommandHandlerAuditLogTests
{
    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    [Fact]
    public async Task Handle_Success_CreatesAuditLogWithReporterRole()
    {
        var now = new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var context = new InMemoryApplicationDbContext();
        context.AddSet(new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            Title = "Report contract",
            Status = (int)ContractStatus.Active,
            CreatedAt = now
        });
        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId, CreatedAt = now });
        context.AddSet(new FreelancerProfile { FreelancerProfilesId = freelancerProfileId, UserId = freelancerUserId, CreatedAt = now });
        context.AddSet(new User { UserId = freelancerUserId, FullName = "Freelancer", Email = "f@example.com", Role = (int)UserRole.Freelancer, IsActive = true, CreatedAt = now });
        context.AddSet<ReportContract>();
        context.AddSet<ReportContractAttachment>();
        context.AddSet<Message>();
        context.AddSet<Conversation>();

        var userAuditLog = new CapturingUserAuditLogService();
        var handler = new CreateReportCommandHandler(
            context,
            new FixedDateTimeService(now),
            Substitute.For<IMediaService>(),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier(),
            Substitute.For<ILogger<CreateReportCommandHandler>>(),
            userAuditLog);

        var response = await handler.Handle(
            new CreateReportCommand(
                contractId,
                freelancerUserId,
                (int)ContractReportIssueType.PoorQuality,
                "The client is unresponsive.",
                "Please respond.",
                null,
                []),
            CancellationToken.None);

        var auditEntry = Assert.Single(userAuditLog.Entries);
        Assert.Equal(freelancerUserId, auditEntry.UserId);
        Assert.Equal(UserRole.Freelancer, auditEntry.Role);
        Assert.Equal(AuditUserActionType.ReportCreated, auditEntry.ActionType);
        Assert.Equal(contractId, auditEntry.ContractId);
        Assert.Equal(response.ReportContractId, auditEntry.ReportId);
    }

    [Fact]
    public async Task Handle_ContractDisputed_ThrowsAndCreatesNoAuditLog()
    {
        var now = new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var context = new InMemoryApplicationDbContext();
        context.AddSet(new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            Title = "Disputed contract",
            Status = (int)ContractStatus.Disputed,
            CreatedAt = now
        });
        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId, CreatedAt = now });
        context.AddSet<ReportContract>();

        var userAuditLog = new CapturingUserAuditLogService();
        var handler = new CreateReportCommandHandler(
            context,
            new FixedDateTimeService(now),
            Substitute.For<IMediaService>(),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier(),
            Substitute.For<ILogger<CreateReportCommandHandler>>(),
            userAuditLog);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateReportCommand(
                contractId,
                clientUserId,
                (int)ContractReportIssueType.PoorQuality,
                "Issue.",
                "Fix it.",
                null,
                []),
            CancellationToken.None));

        Assert.Empty(userAuditLog.Entries);
    }
}
