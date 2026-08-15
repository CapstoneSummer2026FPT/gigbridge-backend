using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Elo.Services;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.Elo.Commands.ResolveEloAppeal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Elo;
using Domain.Enums.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Elo;

public sealed class ResolveEloAppealCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FullReversal_ApprovesAppealAndWritesCorrection()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        context.UserEloScores.Add(NewScore(user.UserId, 150));
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        await context.SaveChangesAsync();
        var audit = Substitute.For<IAdminAuditService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = CreateHandler(context, audit, notifications);

        var result = await handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.Approved,
                EloPointAppealResolution.FullReversal, null, "Granting the reversal."),
            CancellationToken.None);

        Assert.Equal((int)EloPointAppealStatus.Approved, result.Status);
        Assert.Equal((int)EloPointAppealResolution.FullReversal, result.Resolution);
        Assert.Equal("Granting the reversal.", result.ResolutionNote);
        Assert.Equal(admin.UserId, result.ReviewedByAdminId);
        Assert.NotNull(result.ReviewedAt);

        var stored = await context.Set<EloPointAppeal>().SingleAsync();
        Assert.NotNull(stored.AppliedTransactionId);

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(110, score.CurrentPoints);

        var correction = await context.UserEloPointTransactions
            .SingleAsync(x => x.Reason == (int)UserEloPointReason.AppealCorrection);
        Assert.Equal(-40, correction.PointsDelta);
        Assert.Equal(appeal.EloPointAppealId, correction.EloAppealId);

        audit.Received(1).Add(
            admin.UserId, "Elo.AppealResolution", nameof(EloPointAppeal),
            appeal.EloPointAppealId, Arg.Any<object>(), Arg.Any<object>());
        await notifications.Received(1).CreateNotificationAsync(
            user.UserId, NotificationType.EloAppealStatusChanged,
            Arg.Any<string>(), Arg.Any<string?>(), appeal.EloPointAppealId,
            nameof(EloPointAppeal), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PartialCorrection_AppliesGivenDelta()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        context.UserEloScores.Add(NewScore(user.UserId, 150));
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.PartiallyApproved,
                EloPointAppealResolution.PartialCorrection, 20, null),
            CancellationToken.None);

        Assert.Equal((int)EloPointAppealStatus.PartiallyApproved, result.Status);
        Assert.Equal(20, result.CorrectedDelta);

        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(170, score.CurrentPoints);
        var correction = await context.UserEloPointTransactions
            .SingleAsync(x => x.Reason == (int)UserEloPointReason.AppealCorrection);
        Assert.Equal(20, correction.PointsDelta);
    }

    [Fact]
    public async Task Rejected_WritesNoCorrection()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        context.UserEloScores.Add(NewScore(user.UserId, 150));
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.Rejected,
                EloPointAppealResolution.NoChange, null, "No grounds found."),
            CancellationToken.None);

        Assert.Equal((int)EloPointAppealStatus.Rejected, result.Status);
        Assert.Equal((int)EloPointAppealResolution.NoChange, result.Resolution);
        var score = await context.UserEloScores.SingleAsync();
        Assert.Equal(150, score.CurrentPoints);
        Assert.Single(await context.UserEloPointTransactions.ToListAsync());
    }

    [Fact]
    public async Task AlreadyResolved_ReturnsExistingAppeal()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        context.UserEloScores.Add(NewScore(user.UserId, 110));
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        appeal.Status = (int)EloPointAppealStatus.Approved;
        appeal.Resolution = (int)EloPointAppealResolution.FullReversal;
        appeal.ReviewedAt = Now;
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.Approved,
                EloPointAppealResolution.FullReversal, null, null),
            CancellationToken.None);

        Assert.Equal((int)EloPointAppealStatus.Approved, result.Status);
        Assert.Single(await context.UserEloPointTransactions.ToListAsync());
    }

    [Fact]
    public async Task CancelledAppeal_Throws()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        appeal.Status = (int)EloPointAppealStatus.Cancelled;
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.Rejected,
                EloPointAppealResolution.NoChange, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task ApprovedWithoutResolution_Throws()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.Approved,
                EloPointAppealResolution.NoChange, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task PartialCorrectionWithoutDelta_Throws()
    {
        await using var context = CreateContext();
        var (admin, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ResolveEloAppealCommand(
                admin.UserId, appeal.EloPointAppealId, EloPointAppealStatus.PartiallyApproved,
                EloPointAppealResolution.PartialCorrection, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task NonAdmin_ThrowsForbidden()
    {
        await using var context = CreateContext();
        var (_, user) = AddUsers(context);
        var original = AddTransaction(context, user.UserId, 40, 110, 150);
        var appeal = AddAppeal(context, user.UserId, original.UserEloPointTransactionsId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new ResolveEloAppealCommand(
                user.UserId, appeal.EloPointAppealId, EloPointAppealStatus.Rejected,
                EloPointAppealResolution.NoChange, null, null),
            CancellationToken.None));
    }

    private static ResolveEloAppealCommandHandler CreateHandler(
        GigbridgeDbContext context,
        IAdminAuditService? audit = null,
        INotificationService? notifications = null)
    {
        var clock = new Clock();
        return new ResolveEloAppealCommandHandler(
            context,
            clock,
            audit ?? Substitute.For<IAdminAuditService>(),
            notifications ?? new NoopNotificationService(),
            new UserEloService(context, clock));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static (User Admin, User User) AddUsers(GigbridgeDbContext context)
    {
        var admin = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Admin User",
            Email = $"{Guid.NewGuid():N}@admin.com",
            Role = (int)UserRole.Admin,
            IsActive = true,
            CreatedAt = Now
        };
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer User",
            Email = $"{Guid.NewGuid():N}@freelancer.com",
            Role = (int)UserRole.Freelancer,
            IsActive = true,
            CreatedAt = Now
        };
        context.Users.AddRange(admin, user);
        return (admin, user);
    }

    private static UserEloScore NewScore(Guid userId, int points) => new()
    {
        UserEloScoresId = Guid.NewGuid(),
        UserId = userId,
        CurrentPoints = points,
        LastActivityAt = Now,
        CreatedAt = Now
    };

    private static UserEloPointTransaction AddTransaction(
        GigbridgeDbContext context, Guid userId, int delta, int before, int after)
    {
        var transaction = new UserEloPointTransaction
        {
            UserEloPointTransactionsId = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = delta,
            PointsBefore = before,
            PointsAfter = after,
            Reason = (int)UserEloPointReason.CompletedJobReview,
            SourceEntityType = "Review",
            SourceEntityId = Guid.NewGuid(),
            IdempotencyKey = $"completed-job-review:{Guid.NewGuid()}:{userId}",
            CreatedAt = Now.AddDays(-2)
        };
        context.UserEloPointTransactions.Add(transaction);
        return transaction;
    }

    private static EloPointAppeal AddAppeal(GigbridgeDbContext context, Guid userId, Guid transactionId)
    {
        var appeal = new EloPointAppeal
        {
            EloPointAppealId = Guid.NewGuid(),
            UserId = userId,
            EloPointTransactionId = transactionId,
            Status = (int)EloPointAppealStatus.Pending,
            Reason = "This change was incorrect.",
            CreatedAt = Now,
            UpdatedAt = Now
        };
        context.Set<EloPointAppeal>().Add(appeal);
        return appeal;
    }

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }
}
