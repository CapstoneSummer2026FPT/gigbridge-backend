using Application.Features.Proposals.Freelancer.QuestionTimers.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.QuestionTimers.Commands;

public record StartProposalQuestionTimerCommand(
    Guid ProposalId,
    Guid JobPostQuestionId,
    Guid UserId) : IRequest<QuestionTimerStateDto>;
