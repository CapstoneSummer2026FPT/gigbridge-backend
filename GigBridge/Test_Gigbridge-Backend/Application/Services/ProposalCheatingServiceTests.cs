using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Services;

public class ProposalCheatingServiceTests
{
    [Fact]
    public async Task LogEventAsync_IsIdempotentForSameClientEventId()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 26, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);
        var request = new LogProposalCheatingEventRequest
        {
            EventType = (int)CheatingEventType.Copy,
            JobPostQuestionId = fixture.Question.JobPostQuestionsId,
            ClientEventId = "copy-1",
            OccurredAt = now
        };

        var first = await service.LogEventAsync(
            fixture.Proposal.ProposalsId,
            fixture.User.UserId,
            request,
            "127.0.0.1",
            "test-agent",
            CancellationToken.None);

        var second = await service.LogEventAsync(
            fixture.Proposal.ProposalsId,
            fixture.User.UserId,
            request,
            "127.0.0.1",
            "test-agent",
            CancellationToken.None);

        Assert.Equal(1, first.TotalSessionEventCount);
        Assert.Equal(1, second.TotalSessionEventCount);
        Assert.Equal(1, await context.ProposalCheatingEvents.CountAsync());
    }

    [Fact]
    public async Task ApplySubmissionPenaltyIfNeededAsync_FirstViolation_DeductsEloWithoutSuspension()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 26, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        AddScore(context, fixture.User.UserId, now, 100);
        context.ProposalCheatingEvents.Add(CreateEvent(fixture, now, CheatingEventType.Paste));
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        var result = await service.ApplySubmissionPenaltyIfNeededAsync(
            fixture.Proposal,
            fixture.User.UserId,
            CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(result);
        Assert.Equal(1, result.ViolationNumber);
        Assert.Equal(-50, result.EloDelta);
        Assert.Null(result.SuspendedUntil);
        Assert.Null(fixture.User.SuspendedUntil);
        Assert.Equal(50, (await context.UserEloScores.SingleAsync()).CurrentPoints);
        Assert.Equal((int)UserEloPointReason.CheatingPenalty, (await context.UserEloPointTransactions.SingleAsync()).Reason);
    }

    [Fact]
    public async Task ApplySubmissionPenaltyIfNeededAsync_ThirdViolation_DeductsEloAndSuspendsUser()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 26, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        AddScore(context, fixture.User.UserId, now, 100);
        AddPreviousViolation(context, fixture.User.UserId, now.AddDays(-2), 1);
        AddPreviousViolation(context, fixture.User.UserId, now.AddDays(-1), 2);
        context.ProposalCheatingEvents.Add(CreateEvent(fixture, now, CheatingEventType.TabSwitch));
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        var result = await service.ApplySubmissionPenaltyIfNeededAsync(
            fixture.Proposal,
            fixture.User.UserId,
            CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.ViolationNumber);
        Assert.Equal((int)CheatingViolationAction.TemporarySuspension, result.Action);
        Assert.Equal(now.AddDays(7), result.SuspendedUntil);
        Assert.Equal(now.AddDays(7), fixture.User.SuspendedUntil);
        Assert.Equal(50, (await context.UserEloScores.SingleAsync()).CurrentPoints);
    }

    private static ProposalCheatingService CreateService(
        GigbridgeDbContext context,
        DateTime now)
    {
        var clock = new FixedDateTimeService(now);
        return new ProposalCheatingService(context, clock, new UserEloService(context, clock));
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GigbridgeDbContext(options);
    }

    private static ProposalFixture AddDraftProposalFixture(GigbridgeDbContext context, DateTime now)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer User",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = now
        };

        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            CreatedAt = now
        };

        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = Guid.NewGuid(),
            Title = "Backend interview",
            Description = "Answer the interview questions.",
            Status = 1,
            CreatedAt = now
        };

        var question = new JobPostQuestion
        {
            JobPostQuestionsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            JobPosts = jobPost,
            QuestionText = "Explain your approach.",
            OrderIndex = 1,
            IsRequired = true,
            CreatedAt = now
        };

        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            JobPosts = jobPost,
            FreelancerProfilesId = profile.FreelancerProfilesId,
            FreelancerProfiles = profile,
            Status = 0,
            SubmittedAt = now
        };

        context.Users.Add(user);
        context.FreelancerProfiles.Add(profile);
        context.JobPosts.Add(jobPost);
        context.JobPostQuestions.Add(question);
        context.Proposals.Add(proposal);

        return new ProposalFixture(user, profile, jobPost, question, proposal);
    }

    private static ProposalCheatingEvent CreateEvent(
        ProposalFixture fixture,
        DateTime now,
        CheatingEventType eventType)
    {
        return new ProposalCheatingEvent
        {
            ProposalCheatingEventsId = Guid.NewGuid(),
            ProposalsId = fixture.Proposal.ProposalsId,
            Proposals = fixture.Proposal,
            FreelancerUserId = fixture.User.UserId,
            FreelancerUser = fixture.User,
            JobPostQuestionsId = fixture.Question.JobPostQuestionsId,
            EventType = (int)eventType,
            ClientEventId = Guid.NewGuid().ToString("N"),
            OccurredAt = now,
            CreatedAt = now
        };
    }

    private static void AddScore(
        GigbridgeDbContext context,
        Guid userId,
        DateTime now,
        int currentPoints)
    {
        context.UserEloScores.Add(new UserEloScore
        {
            UserEloScoresId = Guid.NewGuid(),
            UserId = userId,
            CurrentPoints = currentPoints,
            LastActivityAt = now,
            CreatedAt = now
        });
    }

    private static void AddPreviousViolation(
        GigbridgeDbContext context,
        Guid userId,
        DateTime createdAt,
        int violationNumber)
    {
        context.FreelancerCheatingViolations.Add(new FreelancerCheatingViolation
        {
            FreelancerCheatingViolationsId = Guid.NewGuid(),
            ProposalsId = Guid.NewGuid(),
            FreelancerUserId = userId,
            ViolationNumber = violationNumber,
            TotalEventCount = 1,
            PasteCount = 1,
            Action = (int)CheatingViolationAction.EloPenalty,
            EloDelta = -50,
            CreatedAt = createdAt
        });
    }

    private sealed record ProposalFixture(
        User User,
        FreelancerProfile Profile,
        JobPost JobPost,
        JobPostQuestion Question,
        Proposal Proposal);

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
