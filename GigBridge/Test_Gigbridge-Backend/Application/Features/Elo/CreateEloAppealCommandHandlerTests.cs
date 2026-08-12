using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Features.Elo.Commands.CreateEloAppeal;
using Application.Features.Elo.Common;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Elo;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Elo;

public sealed class CreateEloAppealCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_CreatesAppealAndUploadsEvidence()
    {
        await using var context = CreateContext();
        var user = AddUser(context, UserRole.Freelancer);
        var transaction = AddTransaction(context, user.UserId);
        await context.SaveChangesAsync();
        var media = new FakeMediaService("https://files.example/evidence.png");
        var handler = CreateHandler(context, media);

        var result = await handler.Handle(
            new CreateEloAppealCommand(
                user.UserId,
                transaction.UserEloPointTransactionsId,
                "  This change was unfair.  ",
                [new EloAppealFile(new MemoryStream([1, 2, 3]), "evidence.png", "image/png", 3, null)]),
            CancellationToken.None);

        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(transaction.UserEloPointTransactionsId, result.TransactionId);
        Assert.Equal((int)EloPointAppealStatus.Pending, result.Status);

        var appeal = await context.Set<EloPointAppeal>().SingleAsync();
        Assert.Equal("This change was unfair.", appeal.Reason);
        Assert.Equal(Now, appeal.CreatedAt);
        Assert.Equal(Now, appeal.UpdatedAt);

        var evidence = await context.Set<EloPointAppealEvidence>().SingleAsync();
        Assert.Equal(appeal.EloPointAppealId, evidence.EloPointAppealId);
        Assert.Equal(user.UserId, evidence.UploadedById);
        Assert.Equal("evidence.png", evidence.FileName);
        Assert.Equal("https://files.example/evidence.png", evidence.FileUrl);
        Assert.Single(media.Uploads);
    }

    [Fact]
    public async Task Handle_ReturnsExistingAppealWhenOneIsActive()
    {
        await using var context = CreateContext();
        var user = AddUser(context, UserRole.Freelancer);
        var transaction = AddTransaction(context, user.UserId);
        await context.SaveChangesAsync();
        var first = new CreateEloAppealCommand(
            user.UserId, transaction.UserEloPointTransactionsId, "Reason one", []);
        var handler = CreateHandler(context, new FakeMediaService());

        var initial = await handler.Handle(first, CancellationToken.None);
        var duplicate = await handler.Handle(
            new CreateEloAppealCommand(
                user.UserId, transaction.UserEloPointTransactionsId, "Reason two", []),
            CancellationToken.None);

        Assert.Equal(initial.AppealId, duplicate.AppealId);
        Assert.Single(await context.Set<EloPointAppeal>().ToListAsync());
    }

    [Fact]
    public async Task Handle_RejectsAppealingAnotherUsersTransaction()
    {
        await using var context = CreateContext();
        var owner = AddUser(context, UserRole.Freelancer);
        var other = AddUser(context, UserRole.Client);
        var transaction = AddTransaction(context, owner.UserId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, new FakeMediaService());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new CreateEloAppealCommand(
                other.UserId, transaction.UserEloPointTransactionsId, "Reason", []),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RejectsMissingTransaction()
    {
        await using var context = CreateContext();
        var user = AddUser(context, UserRole.Freelancer);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, new FakeMediaService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateEloAppealCommand(user.UserId, Guid.NewGuid(), "Reason", []),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RejectsInitialGrantTransaction()
    {
        await using var context = CreateContext();
        var user = AddUser(context, UserRole.Freelancer);
        var transaction = AddTransaction(context, user.UserId, reason: UserEloPointReason.InitialGrant);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, new FakeMediaService());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateEloAppealCommand(
                user.UserId, transaction.UserEloPointTransactionsId, "Reason", []),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RejectsBlankOrOversizedReason()
    {
        await using var context = CreateContext();
        var user = AddUser(context, UserRole.Freelancer);
        var transaction = AddTransaction(context, user.UserId);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, new FakeMediaService());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateEloAppealCommand(user.UserId, transaction.UserEloPointTransactionsId, "   ", []),
            CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateEloAppealCommand(
                user.UserId, transaction.UserEloPointTransactionsId, new string('x', 2001), []),
            CancellationToken.None));
    }

    private static CreateEloAppealCommandHandler CreateHandler(GigbridgeDbContext context, IMediaService media)
    {
        return new CreateEloAppealCommandHandler(context, new Clock(), media);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static User AddUser(GigbridgeDbContext context, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = $"{role} User",
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = (int)role,
            IsActive = true,
            CreatedAt = Now
        };
        context.Users.Add(user);
        return user;
    }

    private static UserEloPointTransaction AddTransaction(
        GigbridgeDbContext context,
        Guid userId,
        UserEloPointReason reason = UserEloPointReason.CompletedJobReview)
    {
        var transaction = new UserEloPointTransaction
        {
            UserEloPointTransactionsId = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = 40,
            PointsBefore = 100,
            PointsAfter = 140,
            Reason = (int)reason,
            SourceEntityType = "Review",
            SourceEntityId = Guid.NewGuid(),
            IdempotencyKey = $"completed-job-review:{Guid.NewGuid()}:{userId}",
            CreatedAt = Now.AddDays(-1)
        };
        context.UserEloPointTransactions.Add(transaction);
        return transaction;
    }

    private sealed class Clock : IDateTimeService
    {
        public DateTime UtcNow => Now;
    }
}
