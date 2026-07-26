using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Common.Services;
using Application.Features.AiInterviews.Freelancer.Start.Commands;
using Application.Features.Premium.Client.AiInterviews.Create.Commands;
using Application.Features.Premium.Client.AiInterviews.DTOs;
using Application.Features.Disputes.Client.Create.Commands;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Premium.Client.JobPostPromotion.Commands;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.PremiumClient;

public sealed class PremiumClientCapabilityTests
{
    [Fact]
    public async Task PremiumAccess_RecognizesActiveSharedClientPlan()
    {
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, Role = (int)UserRole.Client, IsEmailVerified = true };
        var plan = new SubscriptionPlan { IsActive = true, Price = 10, TargetRole = null, Name = "Shared Premium" };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        context.AddSet(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(30),
            SubscriptionPlans = plan
        });
        var service = new PremiumAccessService(context, new MemoryCache(), new Clock(now));

        Assert.True(await service.IsPremiumClientAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task PremiumAccess_RejectsExpiredClientPlanWithRequiredMessage()
    {
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, Role = (int)UserRole.Client };
        var plan = new SubscriptionPlan
        {
            IsActive = true,
            Price = 10,
            TargetRole = (int)UserRole.Client,
            Name = "Client Premium"
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        context.AddSet(new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-30),
            EndDate = now.AddMinutes(-1),
            SubscriptionPlans = plan
        });
        var service = new PremiumAccessService(context, new MemoryCache(), new Clock(now));

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.RequirePremiumClientAsync(userId, CancellationToken.None));
        Assert.Equal("This feature requires a Premium subscription", exception.Message);
    }

    [Fact]
    public async Task CreateAiInterview_RegistersActiveDefinitionWithAiService()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var client = new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = userId };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new JobPost
        {
            JobPostsId = jobId,
            ClientProfilesId = client.ClientProfilesId,
            ClientProfiles = client,
            Status = 1,
            Title = "Backend Engineer",
            Description = "Build APIs"
        });
        var definitions = context.AddSet<AiInterviewDefinition>();
        var aiService = Substitute.For<IAiServiceClient>();
        aiService.CreateInterviewDefinitionAsync(
                Arg.Any<AiInterviewDefinitionRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new AiInterviewDefinitionResponseDto
            {
                DefinitionReference = "aidef_test-reference",
                Status = "active",
                Language = "en",
                Mode = "text",
                QuestionCount = 5
            });
        var handler = new CreateAiInterviewCommandHandler(
            context, new Premium(true), aiService, new Clock(now));

        var result = await handler.Handle(new CreateAiInterviewCommand(
            userId, jobId, new CreateAiInterviewRequest("en", "text", 5)), CancellationToken.None);

        Assert.Equal(AiInterviewDefinitionStatus.Active.ToString(), result.Status);
        Assert.Single(definitions.Entities);
        Assert.Equal("aidef_test-reference", result.ExternalReference);
        await aiService.Received(1).CreateInterviewDefinitionAsync(
            Arg.Is<AiInterviewDefinitionRequestDto>(request =>
                request.JobId == jobId.ToString() &&
                request.Language == "en" &&
                request.Mode == "text" &&
                request.QuestionCount == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAiInterview_UsesLatestPremiumDefinitionConfiguration()
    {
        var now = DateTime.UtcNow;
        var freelancerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var freelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = freelancerId
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new JobPost
        {
            JobPostsId = jobId,
            Status = 1,
            Title = "Backend Engineer",
            Description = "Build APIs"
        });
        context.AddSet(new AiInterviewDefinition
        {
            AiInterviewDefinitionsId = definitionId,
            JobPostId = jobId,
            ClientUserId = Guid.NewGuid(),
            Language = "en",
            Mode = "text",
            QuestionCount = 7,
            Status = AiInterviewDefinitionStatus.Active,
            ExternalReference = "aidef_registered-reference",
            CreatedAt = now
        });
        context.AddSet<AiInterviewAttempt>();
        context.AddSet<AiInterviewAnswerResult>();
        context.AddSet(new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = jobId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            FreelancerProfiles = freelancerProfile,
            Status = 1
        });
        var aiService = Substitute.For<IAiServiceClient>();
        aiService.StartInterviewAsync(
                Arg.Any<AiInterviewStartRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new AiInterviewQuestionResponseDto
            {
                SessionId = "session-123",
                QuestionIndex = 1,
                QuestionText = "First question",
                Language = "en"
            });
        var handler = new StartAiInterviewCommandHandler(context, aiService, new Clock(now));

        await handler.Handle(
            new StartAiInterviewCommand(freelancerId, jobId, null, "voice", "auto"),
            CancellationToken.None);

        await aiService.Received(1).StartInterviewAsync(
            Arg.Is<AiInterviewStartRequestDto>(request =>
                request.Mode == "text" &&
                request.Language == "en" &&
                request.QuestionCount == 7 &&
                request.DefinitionReference == "aidef_registered-reference"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAiInterview_RequiresSubmittedProposal()
    {
        var now = DateTime.UtcNow;
        var freelancerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new JobPost
        {
            JobPostsId = jobId,
            Status = 1,
            Title = "Backend Engineer",
            Description = "Build APIs"
        });
        context.AddSet(new AiInterviewDefinition
        {
            AiInterviewDefinitionsId = Guid.NewGuid(),
            JobPostId = jobId,
            ClientUserId = Guid.NewGuid(),
            Language = "en",
            Mode = "voice",
            QuestionCount = 5,
            Status = AiInterviewDefinitionStatus.Active,
            ExternalReference = "aidef_registered-reference",
            CreatedAt = now
        });
        context.AddSet<Proposal>();
        context.AddSet<AiInterviewAttempt>();
        context.AddSet<AiInterviewAnswerResult>();
        var aiService = Substitute.For<IAiServiceClient>();
        var handler = new StartAiInterviewCommandHandler(context, aiService, new Clock(now));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new StartAiInterviewCommand(freelancerId, jobId, null, "voice", "en"),
            CancellationToken.None));

        await aiService.DidNotReceive().StartInterviewAsync(
            Arg.Any<AiInterviewStartRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAiTalentMatches_CallerCancellationIsNotConvertedToServiceFailure()
    {
        var now = DateTime.UtcNow;
        var clientUserId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var clientProfile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = clientUserId
        };
        var freelancerUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer",
            Email = "freelancer@example.com",
            IsActive = true
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfile.ClientProfilesId,
            ClientProfiles = clientProfile,
            Status = 1,
            Title = "Backend Engineer",
            Description = "Build APIs"
        });
        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = freelancerUser.UserId,
            User = freelancerUser,
            Availability = 0
        });
        var runs = context.AddSet<TalentMatchRun>();
        context.AddSet<TalentMatchResult>();
        using var cancellation = new CancellationTokenSource();
        var aiService = Substitute.For<IAiServiceClient>();
        aiService.RerankTalentAsync(Arg.Any<TalentRerankRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromException<TalentRerankResponseDto>(new TaskCanceledException());
            });
        var configuration = Substitute.For<IConfiguration>();
        configuration["FeatureFlags:AiSmartTalentMatchingV1"].Returns("true");
        var handler = new GetAiTalentMatchesQueryHandler(
            context,
            new Premium(true),
            new Clock(now),
            aiService,
            configuration,
            Substitute.For<ILogger<GetAiTalentMatchesQueryHandler>>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(
            new GetAiTalentMatchesQuery(clientUserId, jobPostId), cancellation.Token));

        var run = Assert.Single(runs.Entities);
        Assert.Equal((int)TalentMatchRunStatus.Running, run.Status);
        Assert.Null(run.FailureCode);
        Assert.Equal(0, context.SaveChangesCount);
    }

    [Fact]
    public async Task CreateDispute_AutoPrioritizesPremiumClientForTwentyFourHours()
    {
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new Contract
        {
            ContractsId = contractId,
            Status = (int)ContractStatus.Active,
            ClientProfiles = new ClientProfile { UserId = userId },
            Title = "Contract"
        });
        var disputes = context.AddSet<Dispute>();
        var handler = new CreateDisputeCommandHandler(context, new Premium(true), new Clock(now));

        var result = await handler.Handle(new CreateDisputeCommand(
            userId, new CreateDisputeRequest(contractId, null, "Delivery does not match requirements")),
            CancellationToken.None);

        Assert.True(result.IsVipPriority);
        Assert.Equal(now.AddHours(24), result.ResolutionTargetAt);
        Assert.Equal(DisputeAiAnalysisStatus.Unavailable.ToString(), result.AiAnalysisStatus);
        Assert.Single(disputes.Entities);
    }

    [Fact]
    public async Task PromoteJobPost_IsIdempotentAndDebitsOnce()
    {
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var client = new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = userId };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new JobPost
        {
            JobPostsId = jobId,
            ClientProfilesId = client.ClientProfilesId,
            ClientProfiles = client,
            Status = 1,
            Title = "Backend Engineer",
            Description = "Build APIs"
        });
        var promotions = context.AddSet<JobPostPromotion>();
        context.AddSet<PlatformSetting>();
        var ledger = new Ledger(userId);
        var handler = new PromoteJobPostCommandHandler(
            context, new Premium(true), ledger, new Clock(now));
        var command = new PromoteJobPostCommand(
            userId, jobId, new PromoteJobPostRequest(
                "promote-request-1",
                "https://cdn.gigbridge.test/promotions/job.png",
                "Build a premium marketplace",
                "Join this client project and ship a polished marketplace experience."));

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.WalletTransactionId, second.WalletTransactionId);
        Assert.Equal(1, ledger.DebitCount);
        Assert.Single(promotions.Entities);
        Assert.Equal(10m, first.TokenCost);
        Assert.Equal(now.AddDays(7), first.FeaturedUntil);
    }

    private sealed class Clock(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? (T?)value : default);
        public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
        {
            _values[key] = value!;
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class Premium(bool isPremium) : IPremiumAccessService
    {
        public Task<bool> IsPremiumFreelancerAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsPremiumClientAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(isPremium);
        public Task<global::Application.Features.Premium.Common.PremiumBenefitsDto> GetPremiumBenefitsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RequirePremiumFreelancerAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RequirePremiumClientAsync(Guid userId, CancellationToken cancellationToken) =>
            isPremium ? Task.CompletedTask : Task.FromException(new ForbiddenAccessException("This feature requires a Premium subscription"));
    }

    private sealed class Ledger(Guid userId) : IWalletLedgerService
    {
        public int DebitCount { get; private set; }
        public Task<WalletTransaction> DebitAsync(Guid requestedUserId, decimal tokenAmount,
            WalletTransactionType type, string idempotencyKey, string? metadata,
            CancellationToken cancellationToken)
        {
            DebitCount++;
            return Task.FromResult(new WalletTransaction
            {
                WalletTransactionsId = Guid.NewGuid(),
                UserId = userId,
                TokenAmount = tokenAmount,
                Type = (int)type,
                IdempotencyKey = idempotencyKey
            });
        }
    }
}
