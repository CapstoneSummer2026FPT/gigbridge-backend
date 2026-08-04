using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.WorkExperiences.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.WorkExperiences.UpdateWorkExperience.Commands;

public sealed class UpdateWorkExperienceCommandHandler
    : IRequestHandler<UpdateWorkExperienceCommand, WorkExperienceDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateWorkExperienceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkExperienceDto> Handle(
        UpdateWorkExperienceCommand request,
        CancellationToken cancellationToken)
    {
        var experience = await _context.Set<WorkExperience>()
            .Include(item => item.Freelancer)
            .FirstOrDefaultAsync(
                item => item.WorkExperiencesId == request.WorkExperienceId &&
                    item.Freelancer.UserId == request.UserId,
                cancellationToken);
        if (experience is null)
        {
            throw new NotFoundException(nameof(WorkExperience), request.WorkExperienceId);
        }

        experience.CompanyName = request.Dto.CompanyName.Trim();
        experience.Title = request.Dto.JobTitle.Trim();
        experience.StartDate = request.Dto.StartDate;
        experience.EndDate = request.Dto.EndDate;
        experience.Description = WorkExperienceMapping.NormalizeOptional(request.Dto.Description);

        await _context.SaveChangesAsync(cancellationToken);

        return experience.ToDto();
    }
}
