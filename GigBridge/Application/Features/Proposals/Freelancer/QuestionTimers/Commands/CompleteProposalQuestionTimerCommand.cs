using Application.Common.InternalServices.Proposals.Models;
using MediatR;

namespace Application.Features.Proposals.Freelancer.QuestionTimers.Commands;

public record CompleteProposalQuestionTimerCommand(
    Guid ProposalId,
    Guid JobPostQuestionId,
    Guid UserId,
    CompleteQuestionTimerRequest Request) : IRequest<QuestionTimerStateDto>;
