using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Requirement;

public sealed record AiInterviewRequirementDto(
    bool Required,
    bool Completed,
    bool InProgress,
    Guid? InterviewDefinitionId);

public sealed record GetAiInterviewRequirementQuery(Guid UserId, Guid JobPostId)
    : IRequest<AiInterviewRequirementDto>;
