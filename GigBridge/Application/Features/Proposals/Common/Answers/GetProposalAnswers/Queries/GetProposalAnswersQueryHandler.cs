using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common.Answers.GetProposalAnswers.Queries;

public class GetProposalAnswersQueryHandler
    : IRequestHandler<GetProposalAnswersQuery, IEnumerable<ProposalAnswerDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProposalAnswersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProposalAnswerDto>> Handle(
        GetProposalAnswersQuery request,
        CancellationToken cancellationToken)
    {
        var proposal = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(proposal => proposal.JobPosts)
            .FirstOrDefaultAsync(proposal => proposal.ProposalsId == request.ProposalsId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        await EnsureCanViewAnswersAsync(proposal, request, cancellationToken);

        var questions = await _context.Set<JobPostQuestion>()
            .AsNoTracking()
            .Where(question => question.JobPostsId == proposal.JobPostsId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync(cancellationToken);

        var answers = await _context.Set<ProposalAnswer>()
            .AsNoTracking()
            .Where(answer => answer.ProposalsId == request.ProposalsId)
            .ToListAsync(cancellationToken);

        var answersByQuestionId = answers.ToDictionary(answer => answer.JobPostQuestionsId);

        return questions.Select(question =>
        {
            answersByQuestionId.TryGetValue(question.JobPostQuestionsId, out var answer);

            return new ProposalAnswerDto(
                answer?.ProposalAnswersId,
                request.ProposalsId,
                question.JobPostQuestionsId,
                question.QuestionText,
                question.OrderIndex,
                question.IsRequired,
                answer?.AnswerText,
                answer?.CreatedAt,
                answer?.UpdatedAt);
        }).ToList();
    }

    private async Task EnsureCanViewAnswersAsync(
        Proposal proposal,
        GetProposalAnswersQuery request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Role, nameof(UserRole.Client), StringComparison.OrdinalIgnoreCase))
        {
            var clientProfile = await _context.Set<ClientProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

            if (clientProfile is null)
            {
                throw new NotFoundException("Client profile does not exist.");
            }

            if (proposal.JobPosts.ClientProfilesId != clientProfile.ClientProfilesId)
            {
                throw new ForbiddenAccessException("You do not have permission to view these answers.");
            }

            return;
        }

        if (string.Equals(request.Role, nameof(UserRole.Freelancer), StringComparison.OrdinalIgnoreCase))
        {
            var freelancerProfile = await _context.Set<FreelancerProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

            if (freelancerProfile is null)
            {
                throw new NotFoundException("Freelancer profile does not exist.");
            }

            if (proposal.FreelancerProfilesId != freelancerProfile.FreelancerProfilesId)
            {
                throw new ForbiddenAccessException("You do not have permission to view these answers.");
            }

            return;
        }

        throw new ForbiddenAccessException("You do not have permission to view these answers.");
    }
}
