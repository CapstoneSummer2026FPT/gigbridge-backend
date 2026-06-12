using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.Answers.UpdateProposalAnswer.Commands;

public class UpdateProposalAnswerCommandHandler
    : IRequestHandler<UpdateProposalAnswerCommand, ProposalAnswerDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateProposalAnswerCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ProposalAnswerDto> Handle(
        UpdateProposalAnswerCommand command,
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

        var answer = await _context.Set<ProposalAnswer>()
            .Include(answer => answer.JobPostQuestions)
            .FirstOrDefaultAsync(
                answer => answer.ProposalAnswersId == command.ProposalAnswersId,
                cancellationToken);

        if (answer is null)
        {
            throw new NotFoundException("Answer does not exist.");
        }

        if (answer.ProposalsId != command.ProposalsId)
        {
            throw new BadRequestException("Answer does not belong to this proposal.");
        }

        ProposalAnswerCommandHelper.EnsureRequiredQuestionHasAnswer(
            answer.JobPostQuestions,
            command.Request.AnswerText);

        answer.AnswerText = ProposalAnswerCommandHelper.NormalizeAnswerText(command.Request.AnswerText);
        answer.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return ProposalAnswerCommandHelper.ToDto(answer, answer.JobPostQuestions);
    }
}
