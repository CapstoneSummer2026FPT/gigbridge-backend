using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FreelancerProfileEntity = Domain.Entities.FreelancerProfile;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Handlers;

public class GetFreelancerProfileQueryHandler 
    : IRequestHandler<Queries.GetFreelancerProfileQuery, FreelancerProfileDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetFreelancerProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FreelancerProfileDetailDto> Handle(
        Queries.GetFreelancerProfileQuery request, 
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfileEntity>()
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.FreelancerSkills)
                .ThenInclude(fs => fs.Skills)
            .Include(p => p.PortfolioItems)
            .Include(p => p.WorkExperiences)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (freelancerProfile == null)
        {
            throw new NotFoundException("FreelancerProfile", request.UserId);
        }

        // Get average review rating
        var avgRating = await _context.Set<Review>()
            .AsNoTracking()
            .Where(r => r.RevieweeId == request.UserId)
            .AverageAsync(r => (double?)r.Rating, cancellationToken) ?? 0.0;

        var detailDto = new FreelancerProfileDetailDto
        {
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            UserId = freelancerProfile.UserId,
            Title = freelancerProfile.Title,
            Bio = freelancerProfile.Bio,
            HourlyRate = freelancerProfile.HourlyRate,
            ExperienceLevel = freelancerProfile.ExperienceLevel,
            Availability = freelancerProfile.Availability,
            Location = freelancerProfile.Location,
            ProfileCompletionScore = freelancerProfile.ProfileCompletionScore,
            CreatedAt = freelancerProfile.CreatedAt,
            UpdatedAt = freelancerProfile.UpdatedAt,

            UserFullName = freelancerProfile.User.FullName,
            UserEmail = freelancerProfile.User.Email,
            UserAvatar = freelancerProfile.User.Avatar,
            Rating = Math.Round(avgRating, 1),

            Skills = freelancerProfile.FreelancerSkills.Select(fs => new FreelancerSkillDto
            {
                SkillId = fs.SkillsId,
                SkillName = fs.Skills?.Name ?? string.Empty,
                ProficiencyLevel = fs.ProficiencyLevel
            }).ToList(),

            PortfolioItems = freelancerProfile.PortfolioItems.Select(pi => new PortfolioItemDto
            {
                PortfolioItemId = pi.PortfolioItemsId,
                ProjectUrl = pi.ProjectUrl
            }).ToList(),

            WorkExperiences = freelancerProfile.WorkExperiences.Select(we => new WorkExperienceDto
            {
                WorkExperienceId = we.WorkExperiencesId,
                CompanyName = we.CompanyName,
                JobTitle = we.Title,
                Description = we.Description,
                StartDate = we.StartDate.ToString("yyyy-MM-dd"),
                EndDate = we.EndDate?.ToString("yyyy-MM-dd")
            }).ToList()
        };

        return detailDto;
    }
}
