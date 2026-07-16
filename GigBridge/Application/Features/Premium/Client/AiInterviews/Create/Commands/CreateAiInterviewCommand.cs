using Application.Features.Premium.Client.AiInterviews.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.AiInterviews.Create.Commands;

public sealed record CreateAiInterviewCommand(
    Guid UserId,
    Guid JobPostId,
    CreateAiInterviewRequest Request) : IRequest<AiInterviewDefinitionDto>;
