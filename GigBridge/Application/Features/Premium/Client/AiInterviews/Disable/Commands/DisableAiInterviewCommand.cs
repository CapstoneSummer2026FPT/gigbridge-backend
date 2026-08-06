using MediatR;

namespace Application.Features.Premium.Client.AiInterviews.Disable.Commands;

public sealed record DisableAiInterviewCommand(
    Guid UserId,
    Guid JobPostId) : IRequest<bool>;
