using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common.Services;

public sealed class UserAuditLogServiceTests
{
    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    [Fact]
    public void Add_PopulatesAllFieldsAndUsesServerClock()
    {
        var context = new InMemoryApplicationDbContext();
        var logs = context.AddSet<AuditLogUser>();
        var now = new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc);
        var service = new UserAuditLogService(context, new FixedDateTimeService(now));

        var userId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var relatedEntityId = Guid.NewGuid();

        service.Add(
            userId,
            UserRole.Freelancer,
            AuditUserActionType.MilestoneSubmitted,
            contractId,
            "Submitted milestone: M1.",
            jobPostId: jobPostId,
            milestoneId: milestoneId,
            reportId: reportId,
            disputeId: disputeId,
            relatedEntityId: relatedEntityId,
            relatedEntityType: "TestEntity");

        var entry = Assert.Single(logs.Entities);
        Assert.NotEqual(Guid.Empty, entry.AuditLogUsersId);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal((int)UserRole.Freelancer, entry.UserRole);
        Assert.Equal((int)AuditUserActionType.MilestoneSubmitted, entry.ActionType);
        Assert.Equal(contractId, entry.ContractId);
        Assert.Equal(jobPostId, entry.JobPostId);
        Assert.Equal(milestoneId, entry.MilestoneId);
        Assert.Equal(reportId, entry.ReportId);
        Assert.Equal(disputeId, entry.DisputeId);
        Assert.Equal(relatedEntityId, entry.RelatedEntityId);
        Assert.Equal("TestEntity", entry.RelatedEntityType);
        Assert.Equal("Submitted milestone: M1.", entry.Description);
        Assert.Equal(now, entry.CreatedAt);
    }

    [Fact]
    public void Add_WithoutOptionalFields_LeavesThemNull()
    {
        var context = new InMemoryApplicationDbContext();
        var logs = context.AddSet<AuditLogUser>();
        var now = new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc);
        var service = new UserAuditLogService(context, new FixedDateTimeService(now));

        service.Add(
            Guid.NewGuid(),
            UserRole.Client,
            AuditUserActionType.EscrowFunded,
            Guid.NewGuid(),
            "Funded contract escrow.");

        var entry = Assert.Single(logs.Entities);
        Assert.Null(entry.JobPostId);
        Assert.Null(entry.MilestoneId);
        Assert.Null(entry.ReportId);
        Assert.Null(entry.DisputeId);
        Assert.Null(entry.RelatedEntityId);
        Assert.Null(entry.RelatedEntityType);
    }
}
