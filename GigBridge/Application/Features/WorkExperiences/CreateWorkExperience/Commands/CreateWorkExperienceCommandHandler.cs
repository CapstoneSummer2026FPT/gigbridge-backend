using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.WorkExperiences.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.WorkExperiences.CreateWorkExperience.Commands;

public sealed class CreateWorkExperienceCommandHandler
    : IRequestHandler<CreateWorkExperienceCommand, WorkExperienceDto>
{
    private readonly IApplicationDbContext _context;

    public CreateWorkExperienceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkExperienceDto> Handle(
        CreateWorkExperienceCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(nameof(FreelancerProfile), request.UserId);
        }

        var experience = new WorkExperience
        {
            WorkExperiencesId = Guid.NewGuid(),
            FreelancerId = profile.FreelancerProfilesId,
            CompanyName = request.Dto.CompanyName.Trim(),
            Title = request.Dto.JobTitle.Trim(),
            StartDate = request.Dto.StartDate,
            EndDate = request.Dto.EndDate,
            Description = WorkExperienceMapping.NormalizeOptional(request.Dto.Description)
        };

        _context.Set<WorkExperience>().Add(experience);
        await _context.SaveChangesAsync(cancellationToken);

        return experience.ToDto();
    }
}
