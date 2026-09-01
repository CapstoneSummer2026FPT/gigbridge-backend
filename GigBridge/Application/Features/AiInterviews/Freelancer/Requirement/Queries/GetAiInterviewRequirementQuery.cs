using System;
using Application.Features.AiInterviews.Freelancer.Requirement.DTOs;
using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Requirement.Queries;

public sealed record GetAiInterviewRequirementQuery(Guid UserId, Guid JobPostId)
    : IRequest<AiInterviewRequirementDto>;
