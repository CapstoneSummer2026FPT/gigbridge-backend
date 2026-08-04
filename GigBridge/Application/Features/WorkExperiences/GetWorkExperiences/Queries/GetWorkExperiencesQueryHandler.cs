using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.WorkExperiences.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.WorkExperiences.GetWorkExperiences.Queries;

public sealed class GetWorkExperiencesQueryHandler
    : IRequestHandler<GetWorkExperiencesQuery, IReadOnlyList<WorkExperienceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWorkExperiencesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkExperienceDto>> Handle(
        GetWorkExperiencesQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(nameof(FreelancerProfile), request.UserId);
        }

        var experiences = await _context.Set<WorkExperience>()
            .AsNoTracking()
            .Where(experience => experience.FreelancerId == profile.FreelancerProfilesId)
            .OrderByDescending(experience => experience.StartDate)
            .ThenByDescending(experience => experience.EndDate)
            .ThenBy(experience => experience.WorkExperiencesId)
            .ToListAsync(cancellationToken);

        return experiences.Select(experience => experience.ToDto()).ToList();
    }
}
