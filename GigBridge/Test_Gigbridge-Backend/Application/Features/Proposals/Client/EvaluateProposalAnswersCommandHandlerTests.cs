using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Proposals.Client.EvaluateProposalAnswers;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Client;

public class EvaluateProposalAnswersCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithCompletedAnswers_SendsOnlySubstantiveAnswersToAi()
    {
        var fixture = CreateFixture("  A concrete answer.  ", "   ");
        var expected = new VettingEvaluationResponseDto { Score = 84 };
        fixture.AiClient
            .AnalyzeVettingAsync(Arg.Any<AnalyzeVettingRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await fixture.Handler.Handle(
            new EvaluateProposalAnswersCommand
            {
                ProposalId = fixture.ProposalId,
                UserId = fixture.ClientUserId
            },
            CancellationToken.None);

        Assert.Same(expected, result);
        await fixture.AiClient.Received(1).AnalyzeVettingAsync(
            Arg.Is<AnalyzeVettingRequestDto>(request =>
                request.FreelancerId == fixture.FreelancerUserId.ToString() &&
                request.QaPairs.Count == 1 &&
                request.QaPairs[0].CandidateAnswer == "  A concrete answer.  "),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData()]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutCompletedAnswers_RejectsBeforeCallingAi(params string?[] answerTexts)
    {
        var fixture = CreateFixture(answerTexts);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Handler.Handle(
                new EvaluateProposalAnswersCommand
                {
                    ProposalId = fixture.ProposalId,
                    UserId = fixture.ClientUserId
                },
                CancellationToken.None));

        Assert.Equal("No completed clarifying answers are available for evaluation.", exception.Message);
        await fixture.AiClient.DidNotReceive().AnalyzeVettingAsync(
            Arg.Any<AnalyzeVettingRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture(params string?[] answerTexts)
    {
        answerTexts ??= [];
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();

        var clientProfile = new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = clientUserId
        };
        var freelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = freelancerUserId
        };
        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            ClientProfiles = clientProfile,
            Title = "Marketplace build",
            Description = "Build a professional marketplace."
        };
        var proposal = new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = jobPostId,
            JobPosts = jobPost,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            FreelancerProfiles = freelancerProfile
        };

        context.AddSet(clientProfile);
        context.AddSet(proposal);
        context.AddSet(answerTexts.Select((answerText, index) =>
        {
            var question = new JobPostQuestion
            {
                JobPostQuestionsId = Guid.NewGuid(),
                JobPostsId = jobPostId,
                JobPosts = jobPost,
                QuestionText = $"Question {index + 1}",
                OrderIndex = index
            };
            return new ProposalAnswer
            {
                ProposalAnswersId = Guid.NewGuid(),
                ProposalsId = proposalId,
                Proposals = proposal,
                JobPostQuestionsId = question.JobPostQuestionsId,
                JobPostQuestions = question,
                AnswerText = answerText!
            };
        }).ToArray());

        var aiClient = Substitute.For<IAiServiceClient>();
        return new Fixture(
            new EvaluateProposalAnswersCommandHandler(context, aiClient),
            aiClient,
            clientUserId,
            freelancerUserId,
            proposalId);
    }

    private sealed record Fixture(
        EvaluateProposalAnswersCommandHandler Handler,
        IAiServiceClient AiClient,
        Guid ClientUserId,
        Guid FreelancerUserId,
        Guid ProposalId);
}
