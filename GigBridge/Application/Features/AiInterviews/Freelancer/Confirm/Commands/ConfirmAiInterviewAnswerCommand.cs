using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Confirm.Commands;

public sealed record ConfirmAiInterviewAnswerCommand(
    Guid UserId,
    string SessionId,
    string? CorrectedText) : IRequest<AiInterviewQuestionResponseDto>;
