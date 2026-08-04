using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.WorkExperiences.Common.DTOs;
using MediatR;

namespace Application.Features.WorkExperiences.UpdateWorkExperience.Commands;

public sealed record UpdateWorkExperienceCommand(
    Guid UserId,
    Guid WorkExperienceId,
    WorkExperienceInputDto Dto) : IRequest<WorkExperienceDto>;
