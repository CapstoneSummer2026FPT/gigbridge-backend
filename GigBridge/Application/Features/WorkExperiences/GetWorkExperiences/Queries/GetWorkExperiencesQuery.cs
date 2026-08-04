using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.WorkExperiences.GetWorkExperiences.Queries;

public sealed record GetWorkExperiencesQuery(Guid UserId)
    : IRequest<IReadOnlyList<WorkExperienceDto>>;
