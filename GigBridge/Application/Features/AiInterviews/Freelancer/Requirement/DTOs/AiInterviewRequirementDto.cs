using System;

namespace Application.Features.AiInterviews.Freelancer.Requirement.DTOs;

public sealed record AiInterviewRequirementDto(
    bool Required,
    bool Completed,
    bool InProgress,
    Guid? InterviewDefinitionId);
