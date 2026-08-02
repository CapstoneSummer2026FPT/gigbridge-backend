using Application.Common.Mappings;
using Application.Features.Admin.Users.GetAllUser.Queries;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Users;

public class GetAllUsersQueryHandlerTests
{
    [Fact]
    public async Task Handle_MarksClientsAndFreelancersWithRoleCompatiblePaidSubscriptionsAsPremium()
    {
        await using var context = CreateContext();
        var premiumFreelancer = AddUser(context, "Premium Freelancer", UserRole.Freelancer);
        var freeFreelancer = AddUser(context, "Free Freelancer", UserRole.Freelancer);
        var premiumClient = AddUser(context, "Premium Client", UserRole.Client);
        var wrongPlanClient = AddUser(context, "Wrong Plan Client", UserRole.Client);
        var admin = AddUser(context, "Admin", UserRole.Admin);

        var freelancerPlan = AddPlan(context, "Freelancer Premium", 150m, UserRole.Freelancer);
        var clientPlan = AddPlan(context, "Client Premium", 150m, UserRole.Client);
        var freePlan = AddPlan(context, "Free", 0m, UserRole.Freelancer);
        AddSubscription(context, premiumFreelancer, freelancerPlan);
        AddSubscription(context, freeFreelancer, freePlan);
        AddSubscription(context, premiumClient, clientPlan);
        AddSubscription(context, wrongPlanClient, freelancerPlan);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context, CreateMapper());
        var result = await handler.Handle(new GetAllUsersQuery(PageSize: 10), CancellationToken.None);

        Assert.Equal(2, result.Items.Count(user => user.IsPremium));
        Assert.True(result.Items.Single(user => user.UserId == premiumFreelancer.UserId).IsPremium);
        Assert.True(result.Items.Single(user => user.UserId == premiumClient.UserId).IsPremium);
        Assert.False(result.Items.Single(user => user.UserId == freeFreelancer.UserId).IsPremium);
        Assert.False(result.Items.Single(user => user.UserId == wrongPlanClient.UserId).IsPremium);
        Assert.False(result.Items.Single(user => user.UserId == admin.UserId).IsPremium);
    }

    [Fact]
    public async Task Handle_PremiumFilterIncludesRoleCompatibleClientsAndFreelancers()
    {
        await using var context = CreateContext();
        var premiumFreelancer = AddUser(context, "Premium Freelancer", UserRole.Freelancer);
        var premiumClient = AddUser(context, "Premium Client", UserRole.Client);
        var freeClient = AddUser(context, "Free Client", UserRole.Client);
        var freelancerPlan = AddPlan(context, "Freelancer Premium", 150m, UserRole.Freelancer);
        var clientPlan = AddPlan(context, "Client Premium", 150m, UserRole.Client);
        AddSubscription(context, premiumFreelancer, freelancerPlan);
        AddSubscription(context, premiumClient, clientPlan);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context, CreateMapper());
        var result = await handler.Handle(
            new GetAllUsersQuery(PageSize: 10, Premium: true), CancellationToken.None);

        Assert.Contains(result.Items, user => user.UserId == premiumFreelancer.UserId);
        Assert.Contains(result.Items, user => user.UserId == premiumClient.UserId);
        Assert.DoesNotContain(result.Items, user => user.UserId == freeClient.UserId);
    }

    [Fact]
    public async Task Handle_AddsOpenReportCountsForPendingAndReviewingUserReports()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, "Reporter", UserRole.Client);
        var reportedUser = AddUser(context, "Reported User", UserRole.Freelancer);
        var cleanUser = AddUser(context, "Clean User", UserRole.Client);

        AddReport(context, reporter.UserId, reportedUser.UserId, ReportStatus.Pending);
        AddReport(context, reporter.UserId, reportedUser.UserId, ReportStatus.Reviewing);
        AddReport(context, reporter.UserId, cleanUser.UserId, ReportStatus.Resolved);
        AddReport(context, reporter.UserId, cleanUser.UserId, ReportStatus.Dismissed);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context, CreateMapper());
        var result = await handler.Handle(new GetAllUsersQuery(PageSize: 10), CancellationToken.None);

        var reported = result.Items.Single(user => user.UserId == reportedUser.UserId);
        var clean = result.Items.Single(user => user.UserId == cleanUser.UserId);

        Assert.Equal(2, reported.OpenReportCount);
        Assert.True(reported.IsCurrentlyReported);
        Assert.Equal(1, result.ReportedUserCount);
        Assert.Equal(0, clean.OpenReportCount);
        Assert.False(clean.IsCurrentlyReported);
    }

    [Fact]
    public async Task Handle_IgnoresOpenReportsForNonUserTargets()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, "Reporter", UserRole.Client);
        var user = AddUser(context, "User", UserRole.Freelancer);

        context.Reports.Add(new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = reporter.UserId,
            ReportedEntityId = user.UserId,
            ReportedEntityType = ReportedEntityTypes.JobPost,
            Type = (int)ReportType.Other,
            Reason = "Wrong target type.",
            Status = (int)ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context, CreateMapper());
        var result = await handler.Handle(new GetAllUsersQuery(PageSize: 10), CancellationToken.None);

        var item = result.Items.Single(resultUser => resultUser.UserId == user.UserId);
        Assert.Equal(0, item.OpenReportCount);
        Assert.False(item.IsCurrentlyReported);
    }

    [Fact]
    public async Task Handle_ReportedUserCountRespectsSearchFilter()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, "Reporter", UserRole.Client);
        var matchingUser = AddUser(context, "Alice", UserRole.Freelancer);
        var nonMatchingUser = AddUser(context, "Bob", UserRole.Freelancer);

        AddReport(context, reporter.UserId, nonMatchingUser.UserId, ReportStatus.Pending);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context, CreateMapper());
        var result = await handler.Handle(
            new GetAllUsersQuery(Search: matchingUser.FullName, PageSize: 10),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(matchingUser.UserId, result.Items[0].UserId);
        Assert.Equal(0, result.ReportedUserCount);
    }

    [Fact]
    public async Task Handle_ReportedUserCountRespectsStatusFilter()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, "Reporter", UserRole.Client);
        var activeUser = AddUser(context, "Active User", UserRole.Freelancer);
        var inactiveUser = AddUser(context, "Inactive User", UserRole.Freelancer);
        inactiveUser.IsActive = false;

        AddReport(context, reporter.UserId, inactiveUser.UserId, ReportStatus.Reviewing);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context, CreateMapper());
        var result = await handler.Handle(
            new GetAllUsersQuery(Status: 1, PageSize: 10),
            CancellationToken.None);

        Assert.Contains(result.Items, user => user.UserId == activeUser.UserId);
        Assert.DoesNotContain(result.Items, user => user.UserId == inactiveUser.UserId);
        Assert.Equal(0, result.ReportedUserCount);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GigbridgeDbContext(options);
    }

    private static IMapper CreateMapper()
    {
        return new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
    }

    private static User AddUser(GigbridgeDbContext context, string fullName, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = fullName,
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = (int)role,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        return user;
    }

    private static SubscriptionPlan AddPlan(
        GigbridgeDbContext context,
        string name,
        decimal price,
        UserRole targetRole)
    {
        var plan = new SubscriptionPlan
        {
            SubscriptionPlansId = Guid.NewGuid(),
            Name = name,
            Price = price,
            DurationInDays = 30,
            TargetRole = (int)targetRole,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.SubscriptionPlans.Add(plan);
        return plan;
    }

    private static void AddSubscription(
        GigbridgeDbContext context,
        User user,
        SubscriptionPlan plan)
    {
        var now = DateTime.UtcNow;
        context.Subscriptions.Add(new Subscription
        {
            SubscriptionsId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            SubscriptionPlansId = plan.SubscriptionPlansId,
            SubscriptionPlans = plan,
            Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(29),
            CreatedAt = now
        });
    }

    private static void AddReport(GigbridgeDbContext context, Guid reporterId, Guid targetId, ReportStatus status)
    {
        context.Reports.Add(new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = reporterId,
            ReportedEntityId = targetId,
            ReportedEntityType = ReportedEntityTypes.User,
            Type = (int)ReportType.Other,
            Reason = "Needs admin review.",
            Status = (int)status,
            CreatedAt = DateTime.UtcNow
        });
    }
}
