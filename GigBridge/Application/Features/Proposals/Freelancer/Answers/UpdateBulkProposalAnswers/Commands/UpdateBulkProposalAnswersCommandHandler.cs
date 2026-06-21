using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.Commands;

public class UpdateBulkProposalAnswersCommandHandler
    : IRequestHandler<UpdateBulkProposalAnswersCommand, IEnumerable<ProposalAnswerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateBulkProposalAnswersCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<IEnumerable<ProposalAnswerDto>> Handle(
        UpdateBulkProposalAnswersCommand command,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await ProposalAnswerCommandHelper.GetFreelancerProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var proposal = await ProposalAnswerCommandHelper.GetFreelancerOwnedProposalAsync(
            _context,
            command.ProposalsId,
            freelancerProfile.FreelancerProfilesId,
            cancellationToken);

        ProposalAnswerCommandHelper.EnsureDraft(proposal);

        var requestItems = command.Request.Answers!;
        var questionIds = requestItems
            .Select(answer => answer.JobPostQuestionId)
            .ToList();

        if (questionIds.Distinct().Count() != questionIds.Count)
        {
            throw new BadRequestException("Answers must not contain duplicate JobPostQuestionId values.");
        }

        var questions = await _context.Set<JobPostQuestion>()
            .Where(question => questionIds.Contains(question.JobPostQuestionsId))
            .ToListAsync(cancellationToken);

        if (questions.Count != questionIds.Count)
        {
            throw new NotFoundException("One or more questions do not exist.");
        }

        if (questions.Any(question => question.JobPostsId != proposal.JobPostsId))
        {
            throw new BadRequestException("One or more questions do not belong to this proposal's job post.");
        }

        var questionsById = questions.ToDictionary(question => question.JobPostQuestionsId);

        foreach (var request in requestItems)
        {
            ProposalAnswerCommandHelper.EnsureRequiredQuestionHasAnswer(
                questionsById[request.JobPostQuestionId],
                request.AnswerText);
        }

        var existingAnswers = await _context.Set<ProposalAnswer>()
            .Where(answer =>
                answer.ProposalsId == command.ProposalsId &&
                questionIds.Contains(answer.JobPostQuestionsId))
            .ToListAsync(cancellationToken);

        var answersByQuestionId = existingAnswers.ToDictionary(answer => answer.JobPostQuestionsId);
        var now = _dateTimeService.UtcNow;
        var changedAnswers = new List<ProposalAnswer>();

        foreach (var request in requestItems)
        {
            if (answersByQuestionId.TryGetValue(request.JobPostQuestionId, out var answer))
            {
                answer.AnswerText = ProposalAnswerCommandHelper.NormalizeAnswerText(request.AnswerText);
                answer.UpdatedAt = now;
                changedAnswers.Add(answer);
                continue;
            }

            var newAnswer = new ProposalAnswer
            {
                ProposalAnswersId = Guid.NewGuid(),
                ProposalsId = command.ProposalsId,
                JobPostQuestionsId = request.JobPostQuestionId,
                AnswerText = ProposalAnswerCommandHelper.NormalizeAnswerText(request.AnswerText),
                CreatedAt = now
            };

            _context.Set<ProposalAnswer>().Add(newAnswer);
            changedAnswers.Add(newAnswer);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return changedAnswers
            .OrderBy(answer => questionsById[answer.JobPostQuestionsId].OrderIndex)
            .Select(answer => ProposalAnswerCommandHelper.ToDto(answer, questionsById[answer.JobPostQuestionsId]))
            .ToList();
    }
}
