using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Freelancer.QuestionTimers.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.QuestionTimers.Commands;

public class StartProposalQuestionTimerCommandHandler
    : IRequestHandler<StartProposalQuestionTimerCommand, QuestionTimerStateDto>
{
    private readonly IProposalQuestionTimerService _timerService;

    public StartProposalQuestionTimerCommandHandler(IProposalQuestionTimerService timerService)
    {
        _timerService = timerService;
    }

    public Task<QuestionTimerStateDto> Handle(
        StartProposalQuestionTimerCommand command,
        CancellationToken cancellationToken)
    {
        return _timerService.StartTimerAsync(
            command.ProposalId,
            command.JobPostQuestionId,
            command.UserId,
            cancellationToken);
    }
}
