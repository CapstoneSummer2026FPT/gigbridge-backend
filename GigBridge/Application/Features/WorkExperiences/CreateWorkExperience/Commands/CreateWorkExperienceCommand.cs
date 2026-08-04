using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.WorkExperiences.Common.DTOs;
using MediatR;

namespace Application.Features.WorkExperiences.CreateWorkExperience.Commands;

public sealed record CreateWorkExperienceCommand(Guid UserId, WorkExperienceInputDto Dto)
    : IRequest<WorkExperienceDto>;
