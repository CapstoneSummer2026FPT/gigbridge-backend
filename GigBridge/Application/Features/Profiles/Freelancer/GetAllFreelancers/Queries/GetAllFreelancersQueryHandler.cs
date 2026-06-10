using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FreelancerProfileEntity = Domain.Entities.FreelancerProfile;

namespace Application.Features.Profiles.FreelancerProfile.GetAllFreelancers.Queries;

public class GetAllFreelancersQueryHandler 
    : IRequestHandler<GetAllFreelancersQuery, IEnumerable<FreelancerProfileDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllFreelancersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<FreelancerProfileDetailDto>> Handle(
        GetAllFreelancersQuery request, 
        CancellationToken cancellationToken)
    {
        // Start query with active users
        IQueryable<FreelancerProfileEntity> query = _context.Set<FreelancerProfileEntity>()
            .AsNoTracking()
            .Include(p => p.User)
                .ThenInclude(u => u.UserEloScore)
            .Include(p => p.FreelancerSkills)
                .ThenInclude(fs => fs.Skills)
            .Include(p => p.PortfolioItems)
            .Include(p => p.WorkExperiences)
            .Where(p => p.User.IsActive);

        // Filter by Availability Status
        if (!string.IsNullOrWhiteSpace(request.AvailabilityStatus))
        {
            var status = request.AvailabilityStatus.ToLowerInvariant();
            if (status == "available")
            {
                query = query.Where(p => p.Availability == 0 || p.Availability == 1);
            }
            else if (status == "busy")
            {
                query = query.Where(p => p.Availability == 1);
            }
            else if (int.TryParse(request.AvailabilityStatus, out var availValue))
            {
                query = query.Where(p => p.Availability == availValue);
            }
        }

        query = query
            .OrderByDescending(p => p.User.UserEloScore != null
                ? p.User.UserEloScore.CurrentPoints
                : UserEloCalculator.DefaultPoints)
            .ThenByDescending(p => p.CreatedAt);

        var freelancerProfiles = await query.ToListAsync(cancellationToken);

        // Fetch all reviews for these freelancers to calculate ratings in memory
        var freelancerUserIds = freelancerProfiles.Select(p => p.UserId).ToList();
        var reviews = await _context.Set<Review>()
            .AsNoTracking()
            .Where(r => freelancerUserIds.Contains(r.RevieweeId))
            .ToListAsync(cancellationToken);

        var reviewsGrouped = reviews
            .GroupBy(r => r.RevieweeId)
            .ToDictionary(g => g.Key, g => g.Average(r => r.Rating));

        // Map to detail DTOs
        var freelancerDtos = freelancerProfiles.Select(fp =>
        {
            reviewsGrouped.TryGetValue(fp.UserId, out var avgRating);
            return new FreelancerProfileDetailDto
            {
                FreelancerProfilesId = fp.FreelancerProfilesId,
                UserId = fp.UserId,
                Title = fp.Title,
                Bio = fp.Bio,
                ExperienceLevel = fp.ExperienceLevel,
                Availability = fp.Availability,
                Location = fp.Location,
                ProfileCompletionScore = fp.ProfileCompletionScore,
                CreatedAt = fp.CreatedAt,
                UpdatedAt = fp.UpdatedAt,

                UserFullName = fp.User.FullName,
                UserEmail = fp.User.Email,
                UserAvatar = fp.User.Avatar,
                Rating = Math.Round(avgRating, 1),
                EloPoints = fp.User.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,

                Skills = fp.FreelancerSkills.Select(fs => new FreelancerSkillDto
                {
                    SkillId = fs.SkillsId,
                    SkillName = fs.Skills?.Name ?? string.Empty,
                    ProficiencyLevel = fs.ProficiencyLevel
                }).ToList(),

                PortfolioItems = fp.PortfolioItems.Select(pi => new PortfolioItemDto
                {
                    PortfolioItemId = pi.PortfolioItemsId,
                    ProjectUrl = pi.ProjectUrl
                }).ToList(),

                WorkExperiences = fp.WorkExperiences.Select(we => new WorkExperienceDto
                {
                    WorkExperienceId = we.WorkExperiencesId,
                    CompanyName = we.CompanyName,
                    JobTitle = we.Title,
                    Description = we.Description,
                    StartDate = we.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = we.EndDate?.ToString("yyyy-MM-dd")
                }).ToList()
            };
        }).ToList();

        // Filter by Skills in memory
        if (request.Skills != null && request.Skills.Count > 0)
        {
            var filterSkills = request.Skills.Select(s => s.ToLowerInvariant()).ToList();
            freelancerDtos = freelancerDtos
                .Where(dto => dto.Skills.Any(s => filterSkills.Contains(s.SkillName.ToLowerInvariant())))
                .ToList();
        }

        // Filter by Min Rating in memory
        if (request.MinRating.HasValue)
        {
            freelancerDtos = freelancerDtos
                .Where(dto => dto.Rating >= request.MinRating.Value)
                .ToList();
        }

        return freelancerDtos;
    }
}
