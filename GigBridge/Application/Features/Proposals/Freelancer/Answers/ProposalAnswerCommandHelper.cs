using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.Answers;

internal static class ProposalAnswerCommandHelper
{
    public static async Task<FreelancerProfile> GetFreelancerProfileAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        return freelancerProfile
            ?? throw new NotFoundException("Freelancer profile does not exist.");
    }

    public static async Task<Proposal> GetFreelancerOwnedProposalAsync(
        IApplicationDbContext context,
        Guid proposalId,
        Guid freelancerProfileId,
        CancellationToken cancellationToken)
    {
        var proposal = await context.Set<Proposal>()
            .FirstOrDefaultAsync(proposal => proposal.ProposalsId == proposalId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        if (proposal.FreelancerProfilesId != freelancerProfileId)
        {
            throw new ForbiddenAccessException("You do not have permission to modify this proposal.");
        }

        return proposal;
    }

    public static void EnsureDraft(Proposal proposal)
    {
        if (proposal.Status != 0)
        {
            throw new BadRequestException("Answers can only be modified while the proposal is draft.");
        }
    }

    public static string NormalizeAnswerText(string? answerText)
    {
        return string.IsNullOrWhiteSpace(answerText)
            ? string.Empty
            : answerText.Trim();
    }

    public static void EnsureRequiredQuestionHasAnswer(JobPostQuestion question, string? answerText)
    {
        if (question.IsRequired && string.IsNullOrWhiteSpace(answerText))
        {
            throw new BadRequestException($"AnswerText is required for question {question.JobPostQuestionsId}.");
        }
    }

    public static ProposalAnswerDto ToDto(
        ProposalAnswer answer,
        JobPostQuestion question)
    {
        return new ProposalAnswerDto(
            answer.ProposalAnswersId,
            answer.ProposalsId,
            question.JobPostQuestionsId,
            question.QuestionText,
            question.OrderIndex,
            question.IsRequired,
            answer.AnswerText,
            answer.CreatedAt,
            answer.UpdatedAt);
    }
}
