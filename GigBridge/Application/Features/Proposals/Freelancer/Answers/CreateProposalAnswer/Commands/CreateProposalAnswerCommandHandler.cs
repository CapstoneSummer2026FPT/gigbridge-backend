using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.Commands;

public class CreateProposalAnswerCommandHandler
    : IRequestHandler<CreateProposalAnswerCommand, ProposalAnswerDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateProposalAnswerCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ProposalAnswerDto> Handle(
        CreateProposalAnswerCommand command,
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

        var question = await _context.Set<JobPostQuestion>()
            .FirstOrDefaultAsync(
                question => question.JobPostQuestionsId == command.Request.JobPostQuestionId,
                cancellationToken);

        if (question is null)
        {
            throw new NotFoundException("Question does not exist.");
        }

        if (question.JobPostsId != proposal.JobPostsId)
        {
            throw new BadRequestException("Question does not belong to this proposal's job post.");
        }

        ProposalAnswerCommandHelper.EnsureRequiredQuestionHasAnswer(question, command.Request.AnswerText);

        var answerExists = await _context.Set<ProposalAnswer>()
            .AnyAsync(
                answer =>
                    answer.ProposalsId == command.ProposalsId &&
                    answer.JobPostQuestionsId == question.JobPostQuestionsId,
                cancellationToken);

        if (answerExists)
        {
            throw new ConflictException("An answer already exists for this question.");
        }

        var answer = new ProposalAnswer
        {
            ProposalAnswersId = Guid.NewGuid(),
            ProposalsId = command.ProposalsId,
            JobPostQuestionsId = question.JobPostQuestionsId,
            AnswerText = ProposalAnswerCommandHelper.NormalizeAnswerText(command.Request.AnswerText),
            CreatedAt = _dateTimeService.UtcNow
        };

        _context.Set<ProposalAnswer>().Add(answer);
        await _context.SaveChangesAsync(cancellationToken);

        return ProposalAnswerCommandHelper.ToDto(answer, question);
    }
}
