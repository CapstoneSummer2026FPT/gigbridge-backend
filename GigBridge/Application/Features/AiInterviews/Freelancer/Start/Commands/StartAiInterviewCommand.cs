using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Start.Commands;

public sealed record StartAiInterviewCommand(
    Guid UserId,
    Guid JobPostId,
    Guid? InterviewDefinitionId,
    string Mode,
    string Language) : IRequest<AiInterviewQuestionResponseDto>;
