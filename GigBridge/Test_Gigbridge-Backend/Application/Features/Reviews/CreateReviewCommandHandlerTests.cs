using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Reviews.Common.CreateReview.Commands;
using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Features.Reviews;

public class CreateReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_CompletedContractCreatesReviewAndAppliesElo()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var seed = await SeedContractAsync(context, ContractStatus.Completed, now);
        var handler = CreateHandler(context, now);

        var response = await handler.Handle(
            new CreateReviewCommand(
                seed.ClientUserId,
                new CreateReviewRequest
                {
                    ContractId = seed.ContractId,
                    Rating = 5,
                    Comment = " Strong delivery. ",
                    CommunicationRating = 4,
                    QualityRating = 5,
                    TimelinessRating = 5,
                    IsAnonymous = true
                }),
            CancellationToken.None);

        Assert.Equal(seed.ContractId, response.ContractId);
        Assert.Equal(seed.FreelancerUserId, response.RevieweeId);
        Assert.Equal(5, response.Rating);
        Assert.Equal("Strong delivery.", response.Comment);
        Assert.False(response.IsVisible);
        Assert.Equal("Anonymous User", response.ReviewerName);

        var review = await context.Reviews.SingleAsync();
        Assert.Equal(seed.ClientUserId, review.ReviewerId);
        Assert.Equal(seed.FreelancerUserId, review.RevieweeId);
        Assert.Equal(4, review.CommunicationRating);
        Assert.Equal(5, review.QualityRating);
        Assert.Equal(5, review.TimelinessRating);

        var score = await context.UserEloScores.SingleAsync(score => score.UserId == seed.FreelancerUserId);
        Assert.Equal(170, score.CurrentPoints);

        var transactions = await context.UserEloPointTransactions
            .Where(transaction => transaction.UserId == seed.FreelancerUserId)
            .OrderBy(transaction => transaction.CreatedAt)
            .ThenBy(transaction => transaction.Reason)
            .ToListAsync();

        Assert.Equal(3, transactions.Count);
        Assert.Contains(transactions, transaction =>
            transaction.Reason == (int)UserEloPointReason.InitialGrant &&
            transaction.PointsDelta == 100 &&
            transaction.PointsAfter == 100);
        Assert.Contains(transactions, transaction =>
            transaction.Reason == (int)UserEloPointReason.JobCompletion &&
            transaction.PointsDelta == 20 &&
            transaction.PointsAfter == 120);
        Assert.Contains(transactions, transaction =>
            transaction.Reason == (int)UserEloPointReason.ReviewRating &&
            transaction.PointsDelta == 50 &&
            transaction.PointsAfter == 170);
    }

    [Fact]
    public async Task Handle_ActiveContractDoesNotCreateReviewOrElo()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var seed = await SeedContractAsync(context, ContractStatus.Active, now);
        var handler = CreateHandler(context, now);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CreateReviewCommand(
                    seed.ClientUserId,
                    new CreateReviewRequest
                    {
                        ContractId = seed.ContractId,
                        Rating = 5,
                        Comment = "Not open yet."
                    }),
                CancellationToken.None));

        Assert.Empty(context.Reviews);
        Assert.Empty(context.UserEloScores);
        Assert.Empty(context.UserEloPointTransactions);
    }

    [Fact]
    public async Task Handle_DuplicateReviewReturnsConflictWithoutApplyingElo()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        var seed = await SeedContractAsync(context, ContractStatus.Completed, now);

        context.Reviews.Add(new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = seed.ContractId,
            ReviewerId = seed.ClientUserId,
            RevieweeId = seed.FreelancerUserId,
            Rating = 4,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, now);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CreateReviewCommand(
                    seed.ClientUserId,
                    new CreateReviewRequest
                    {
                        ContractId = seed.ContractId,
                        Rating = 5,
                        Comment = "Second review."
                    }),
                CancellationToken.None));

        Assert.Single(context.Reviews);
        Assert.Empty(context.UserEloScores);
        Assert.Empty(context.UserEloPointTransactions);
    }

    private static CreateReviewCommandHandler CreateHandler(
        GigbridgeDbContext context,
        DateTime now)
    {
        var dateTimeService = new FixedDateTimeService(now);
        return new CreateReviewCommandHandler(
            context,
            dateTimeService,
            new UserEloService(context, dateTimeService));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GigbridgeDbContext(options);
    }

    private static async Task<ReviewSeed> SeedContractAsync(
        GigbridgeDbContext context,
        ContractStatus status,
        DateTime now)
    {
        var clientUserId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var clientUser = new User
        {
            UserId = clientUserId,
            FullName = "Client User",
            Email = "client@example.com",
            Role = (int)UserRole.Client,
            IsActive = true,
            CreatedAt = now
        };
        var freelancerUser = new User
        {
            UserId = freelancerUserId,
            FullName = "Freelancer User",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer,
            IsActive = true,
            CreatedAt = now
        };
        var clientProfile = new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = clientUserId,
            User = clientUser,
            CreatedAt = now
        };
        var freelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancerUserId,
            User = freelancerUser,
            CreatedAt = now
        };
        var contract = new Contract
        {
            ContractsId = contractId,
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            ClientProfiles = clientProfile,
            FreelancerProfiles = freelancerProfile,
            Title = "Completed contract",
            TotalBudget = 1_000_000m,
            Status = (int)status,
            CompletedAt = status == ContractStatus.Completed ? now : null,
            CreatedAt = now
        };

        context.Users.AddRange(clientUser, freelancerUser);
        context.ClientProfiles.Add(clientProfile);
        context.FreelancerProfiles.Add(freelancerProfile);
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        return new ReviewSeed(clientUserId, freelancerUserId, contractId);
    }

    private sealed record ReviewSeed(
        Guid ClientUserId,
        Guid FreelancerUserId,
        Guid ContractId);

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
