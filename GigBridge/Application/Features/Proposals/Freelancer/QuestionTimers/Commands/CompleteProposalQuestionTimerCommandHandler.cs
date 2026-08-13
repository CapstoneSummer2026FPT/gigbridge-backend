using Application.Features.Proposals.Common.Interfaces;
using Application.Features.Proposals.Freelancer.QuestionTimers.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.QuestionTimers.Commands;

public class CompleteProposalQuestionTimerCommandHandler
    : IRequestHandler<CompleteProposalQuestionTimerCommand, QuestionTimerStateDto>
{
    private readonly IProposalQuestionTimerService _timerService;

    public CompleteProposalQuestionTimerCommandHandler(IProposalQuestionTimerService timerService)
    {
        _timerService = timerService;
    }

    public Task<QuestionTimerStateDto> Handle(
        CompleteProposalQuestionTimerCommand command,
        CancellationToken cancellationToken)
    {
        return _timerService.CompleteTimerAsync(
            command.ProposalId,
            command.JobPostQuestionId,
            command.UserId,
            command.Request,
            cancellationToken);
    }
}
