using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Proposals.Services;
using Application.Common.InternalServices.Proposals.Models;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Proposals;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Proposals;

public class ProposalQuestionTimerServiceTests
{
    [Fact]
    public async Task StartTimerAsync_CreatesTimerWithThreeMinuteExpiry()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        var result = await service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);

        Assert.Equal(now, result.StartedAt);
        Assert.Equal(now.AddMinutes(3), result.ExpiresAt);
        Assert.Equal(180, result.RemainingSeconds);
        Assert.False(result.IsLocked);
        Assert.Equal(1, await context.ProposalQuestionTimers.CountAsync());
    }

    [Fact]
    public async Task StartTimerAsync_ReturnsExistingTimerWithoutResetting()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var clock = new MutableDateTimeService(now);
        var service = new ProposalQuestionTimerService(context, clock);

        var first = await service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);

        clock.UtcNow = now.AddSeconds(30);
        var second = await service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);

        Assert.Equal(first.StartedAt, second.StartedAt);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Equal(150, second.RemainingSeconds);
        Assert.Equal(1, await context.ProposalQuestionTimers.CountAsync());
    }

    [Fact]
    public async Task CompleteTimerAsync_SavesAnswerAndLocksQuestion()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);
        await service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);

        var result = await service.CompleteTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            new CompleteQuestionTimerRequest
            {
                AnswerText = " My answer ",
                LockedReason = (int)QuestionTimerLockedReason.Completed
            },
            CancellationToken.None);

        var answer = await context.ProposalAnswers.SingleAsync();
        var timer = await context.ProposalQuestionTimers.SingleAsync();
        Assert.True(result.IsLocked);
        Assert.Equal((int)QuestionTimerLockedReason.Completed, result.LockedReason);
        Assert.Equal("My answer", answer.AnswerText);
        Assert.True(timer.IsLocked);
        Assert.Equal(now, timer.CompletedAt);
    }

    [Fact]
    public async Task EnsureQuestionCanBeModifiedAsync_ExpiredTimerLocksAndRejectsUpdate()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var clock = new MutableDateTimeService(now);
        var service = new ProposalQuestionTimerService(context, clock);
        await service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);

        clock.UtcNow = now.AddMinutes(3).AddSeconds(1);

        await Assert.ThrowsAsync<BadRequestException>(() => service.EnsureQuestionCanBeModifiedAsync(
            fixture.Proposal,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None));

        var timer = await context.ProposalQuestionTimers.SingleAsync();
        Assert.True(timer.IsLocked);
        Assert.Equal((int)QuestionTimerLockedReason.Timeout, timer.LockedReason);
    }

    [Fact]
    public async Task StartTimerAsync_RejectsNonOwnerFreelancer()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        var otherUser = AddFreelancer(context, now);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            otherUser.UserId,
            CancellationToken.None));
    }

    [Fact]
    public async Task StartTimerAsync_RejectsNonDraftProposal()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        fixture.Proposal.Status = 1;
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        await Assert.ThrowsAsync<BadRequestException>(() => service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None));
    }

    [Fact]
    public async Task StartTimerAsync_RejectsQuestionFromDifferentJobPost()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        var otherQuestion = new JobPostQuestion
        {
            JobPostQuestionsId = Guid.NewGuid(),
            JobPostsId = Guid.NewGuid(),
            QuestionText = "Different job question",
            OrderIndex = 1,
            IsRequired = true,
            CreatedAt = now
        };
        context.JobPostQuestions.Add(otherQuestion);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        await Assert.ThrowsAsync<BadRequestException>(() => service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            otherQuestion.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None));
    }

    [Fact]
    public async Task EnsureProposalReadyForSubmissionAsync_RejectsRequiredQuestionWithoutLockedTimer()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);

        await Assert.ThrowsAsync<BadRequestException>(() => service.EnsureProposalReadyForSubmissionAsync(
            fixture.Proposal,
            fixture.User.UserId,
            CancellationToken.None));
    }

    [Fact]
    public async Task EnsureProposalReadyForSubmissionAsync_AllowsRequiredQuestionTimedOut()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var service = CreateService(context, now);
        await service.StartTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);

        await service.CompleteTimerAsync(
            fixture.Proposal.ProposalsId,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            new CompleteQuestionTimerRequest
            {
                AnswerText = string.Empty,
                LockedReason = (int)QuestionTimerLockedReason.Timeout
            },
            CancellationToken.None);

        await service.EnsureProposalReadyForSubmissionAsync(
            fixture.Proposal,
            fixture.User.UserId,
            CancellationToken.None);
    }

    [Fact]
    public async Task StartReviewAsync_RejectsBeforeInterviewCompleted()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        await context.SaveChangesAsync();

        var reviewService = new ProposalInterviewReviewService(context, new MutableDateTimeService(now));

        await Assert.ThrowsAsync<BadRequestException>(() => reviewService.StartReviewAsync(
            fixture.Proposal.ProposalsId,
            fixture.User.UserId,
            CancellationToken.None));
    }

    [Fact]
    public async Task StartReviewAsync_UsesOneMinutePerNonEmptyAnswer()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        var secondQuestion = AddQuestion(context, fixture.JobPost.JobPostsId, now, 2, isRequired: false);
        await context.SaveChangesAsync();

        context.ProposalQuestionTimers.Add(CreateLockedTimer(fixture, fixture.Question.JobPostQuestionsId, now, QuestionTimerLockedReason.Completed));
        context.ProposalQuestionTimers.Add(CreateLockedTimer(fixture, secondQuestion.JobPostQuestionsId, now, QuestionTimerLockedReason.Completed));
        AddAnswer(context, fixture.Proposal.ProposalsId, fixture.Question.JobPostQuestionsId, "First answer", now);
        AddAnswer(context, fixture.Proposal.ProposalsId, secondQuestion.JobPostQuestionsId, "Second answer", now);
        await context.SaveChangesAsync();

        var reviewService = new ProposalInterviewReviewService(context, new MutableDateTimeService(now));

        var result = await reviewService.StartReviewAsync(
            fixture.Proposal.ProposalsId,
            fixture.User.UserId,
            CancellationToken.None);

        Assert.Equal(2, result.ReviewableQuestionCount);
        Assert.Equal(now.AddMinutes(2), result.ExpiresAt);
        Assert.Equal(120, result.RemainingSeconds);
        Assert.Equal(new[] { fixture.Question.JobPostQuestionsId, secondQuestion.JobPostQuestionsId }, result.ReviewableQuestionIds);
    }

    [Fact]
    public async Task StartReviewAsync_DoesNotResetExistingSession()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        context.ProposalQuestionTimers.Add(CreateLockedTimer(fixture, fixture.Question.JobPostQuestionsId, now, QuestionTimerLockedReason.Completed));
        AddAnswer(context, fixture.Proposal.ProposalsId, fixture.Question.JobPostQuestionsId, "Answer", now);
        await context.SaveChangesAsync();

        var clock = new MutableDateTimeService(now);
        var reviewService = new ProposalInterviewReviewService(context, clock);
        var first = await reviewService.StartReviewAsync(
            fixture.Proposal.ProposalsId,
            fixture.User.UserId,
            CancellationToken.None);

        clock.UtcNow = now.AddSeconds(20);
        var second = await reviewService.StartReviewAsync(
            fixture.Proposal.ProposalsId,
            fixture.User.UserId,
            CancellationToken.None);

        Assert.Equal(first.StartedAt, second.StartedAt);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Equal(40, second.RemainingSeconds);
        Assert.Equal(1, await context.ProposalInterviewReviewSessions.CountAsync());
    }

    [Fact]
    public async Task EnsureQuestionCanBeModifiedAsync_AllowsAnsweredLockedQuestionDuringActiveReview()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        context.ProposalQuestionTimers.Add(CreateLockedTimer(fixture, fixture.Question.JobPostQuestionsId, now, QuestionTimerLockedReason.Completed));
        AddAnswer(context, fixture.Proposal.ProposalsId, fixture.Question.JobPostQuestionsId, "Answer", now);
        await context.SaveChangesAsync();

        var clock = new MutableDateTimeService(now);
        var reviewService = new ProposalInterviewReviewService(context, clock);
        await reviewService.StartReviewAsync(fixture.Proposal.ProposalsId, fixture.User.UserId, CancellationToken.None);
        var timerService = new ProposalQuestionTimerService(context, clock);

        await timerService.EnsureQuestionCanBeModifiedAsync(
            fixture.Proposal,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None);
    }

    [Fact]
    public async Task EnsureQuestionCanBeModifiedAsync_RejectsTimeoutEmptyQuestionDuringReview()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        context.ProposalQuestionTimers.Add(CreateLockedTimer(fixture, fixture.Question.JobPostQuestionsId, now, QuestionTimerLockedReason.Timeout));
        await context.SaveChangesAsync();

        var clock = new MutableDateTimeService(now);
        var reviewService = new ProposalInterviewReviewService(context, clock);
        await reviewService.StartReviewAsync(fixture.Proposal.ProposalsId, fixture.User.UserId, CancellationToken.None);
        var timerService = new ProposalQuestionTimerService(context, clock);

        await Assert.ThrowsAsync<BadRequestException>(() => timerService.EnsureQuestionCanBeModifiedAsync(
            fixture.Proposal,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None));
    }

    [Fact]
    public async Task EnsureQuestionCanBeModifiedAsync_RejectsAfterReviewExpires()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 6, 29, 8, 0, 0, DateTimeKind.Utc);
        var fixture = AddDraftProposalFixture(context, now);
        context.ProposalQuestionTimers.Add(CreateLockedTimer(fixture, fixture.Question.JobPostQuestionsId, now, QuestionTimerLockedReason.Completed));
        AddAnswer(context, fixture.Proposal.ProposalsId, fixture.Question.JobPostQuestionsId, "Answer", now);
        await context.SaveChangesAsync();

        var clock = new MutableDateTimeService(now);
        var reviewService = new ProposalInterviewReviewService(context, clock);
        await reviewService.StartReviewAsync(fixture.Proposal.ProposalsId, fixture.User.UserId, CancellationToken.None);
        clock.UtcNow = now.AddMinutes(1).AddSeconds(1);
        var timerService = new ProposalQuestionTimerService(context, clock);

        await Assert.ThrowsAsync<BadRequestException>(() => timerService.EnsureQuestionCanBeModifiedAsync(
            fixture.Proposal,
            fixture.Question.JobPostQuestionsId,
            fixture.User.UserId,
            CancellationToken.None));

        Assert.True((await context.ProposalInterviewReviewSessions.SingleAsync()).IsLocked);
    }

    private static ProposalQuestionTimerService CreateService(
        GigbridgeDbContext context,
        DateTime now)
    {
        return new ProposalQuestionTimerService(context, new MutableDateTimeService(now));
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
        var user = AddFreelancer(context, now);
        var profile = user.FreelancerProfile!;

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

        context.JobPosts.Add(jobPost);
        context.JobPostQuestions.Add(question);
        context.Proposals.Add(proposal);

        return new ProposalFixture(user, profile, jobPost, question, proposal);
    }

    private static User AddFreelancer(GigbridgeDbContext context, DateTime now)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer User",
            Email = $"{Guid.NewGuid():N}@example.com",
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

        user.FreelancerProfile = profile;
        context.Users.Add(user);
        context.FreelancerProfiles.Add(profile);

        return user;
    }

    private static JobPostQuestion AddQuestion(
        GigbridgeDbContext context,
        Guid jobPostId,
        DateTime now,
        int orderIndex,
        bool isRequired)
    {
        var question = new JobPostQuestion
        {
            JobPostQuestionsId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            QuestionText = $"Question {orderIndex}",
            OrderIndex = orderIndex,
            IsRequired = isRequired,
            CreatedAt = now
        };

        context.JobPostQuestions.Add(question);
        return question;
    }

    private static ProposalQuestionTimer CreateLockedTimer(
        ProposalFixture fixture,
        Guid questionId,
        DateTime now,
        QuestionTimerLockedReason reason)
    {
        return new ProposalQuestionTimer
        {
            ProposalQuestionTimersId = Guid.NewGuid(),
            ProposalsId = fixture.Proposal.ProposalsId,
            JobPostQuestionsId = questionId,
            FreelancerUserId = fixture.User.UserId,
            StartedAt = now.AddMinutes(-3),
            ExpiresAt = now,
            CompletedAt = now,
            IsLocked = true,
            LockedReason = (int)reason,
            CreatedAt = now.AddMinutes(-3),
            UpdatedAt = now
        };
    }

    private static void AddAnswer(
        GigbridgeDbContext context,
        Guid proposalId,
        Guid questionId,
        string answerText,
        DateTime now)
    {
        context.ProposalAnswers.Add(new ProposalAnswer
        {
            ProposalAnswersId = Guid.NewGuid(),
            ProposalsId = proposalId,
            JobPostQuestionsId = questionId,
            AnswerText = answerText,
            CreatedAt = now
        });
    }

    private sealed record ProposalFixture(
        User User,
        FreelancerProfile Profile,
        JobPost JobPost,
        JobPostQuestion Question,
        Proposal Proposal);

    private sealed class MutableDateTimeService : IDateTimeService
    {
        public MutableDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }
}
