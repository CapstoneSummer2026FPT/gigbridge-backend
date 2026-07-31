using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;
using Application.Features.Premium.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FreelancerProfileEntity = Domain.Entities.FreelancerProfile;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;

public class GetFreelancerProfileQueryHandler 
    : IRequestHandler<GetFreelancerProfileQuery, FreelancerProfileDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPremiumAccessService _premiumAccessService;

    public GetFreelancerProfileQueryHandler(
        IApplicationDbContext context,
        IPremiumAccessService premiumAccessService)
    {
        _context = context;
        _premiumAccessService = premiumAccessService;
    }

    public async Task<FreelancerProfileDetailDto> Handle(
        GetFreelancerProfileQuery request, 
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfileEntity>()
            .AsNoTracking()
            .Include(p => p.User)
                .ThenInclude(u => u.UserEloScore)
            .Include(p => p.FreelancerSkills)
                .ThenInclude(fs => fs.Skills)
            .Include(p => p.PortfolioItems)
            .Include(p => p.WorkExperiences)
            .Include(p => p.Major)
            .Include(p => p.FreelancerProfileCategories)
                .ThenInclude(selection => selection.MajorCategory)
                    .ThenInclude(mapping => mapping.Category)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (freelancerProfile == null)
        {
            throw new NotFoundException("FreelancerProfile", request.UserId);
        }

        // Get average review rating
        var avgRating = await _context.Set<Review>()
            .AsNoTracking()
            .Where(r =>
                r.RevieweeId == request.UserId &&
                r.ModerationStatus == (int)ReviewModerationStatus.Active)
            .AverageAsync(r => (double?)r.Rating, cancellationToken) ?? 0.0;

        var premium = await _premiumAccessService.GetPremiumBenefitsAsync(request.UserId, cancellationToken);
        PremiumTierResult? tier = null;
        if (premium.IsPremium)
        {
            var tierSetting = await _context.Set<PlatformSetting>()
                .AsNoTracking()
                .Where(item => item.Key == PremiumTierCalculator.SettingKey)
                .Select(item => item.Value)
                .FirstOrDefaultAsync(cancellationToken);
            tier = PremiumTierCalculator.Calculate(
                freelancerProfile.User.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
                tierSetting);
        }
        var detailDto = new FreelancerProfileDetailDto
        {
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            UserId = freelancerProfile.UserId,
            Title = freelancerProfile.Title,
            Bio = freelancerProfile.Bio,
            Availability = freelancerProfile.Availability,
            Location = freelancerProfile.Location,
            ProfileCompletionScore = freelancerProfile.ProfileCompletionScore,
            CreatedAt = freelancerProfile.CreatedAt,
            UpdatedAt = freelancerProfile.UpdatedAt,
            MajorId = freelancerProfile.MajorId,
            MajorName = freelancerProfile.Major?.Name,
            Categories = freelancerProfile.FreelancerProfileCategories
                .OrderBy(selection => selection.MajorCategory.Category.SortOrder)
                .ThenBy(selection => selection.MajorCategory.Category.Name)
                .Select(selection => new FreelancerProfileCategoryDto
                {
                    MajorCategoryId = selection.MajorCategoryId,
                    CategoryId = selection.MajorCategory.CategoryId,
                    Name = selection.MajorCategory.Category.Name
                })
                .ToList(),

            UserFullName = freelancerProfile.User.FullName,
            UserEmail = freelancerProfile.User.Email,
            UserAvatar = freelancerProfile.User.Avatar,
            Rating = Math.Round(avgRating, 1),
            EloPoints = freelancerProfile.User.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
            IsPremium = premium.IsPremium,
            IsIdentityVerified = premium.IsIdentityVerified,
            ShowProVerifiedBadge = premium.ShowProVerifiedBadge,
            PremiumUntil = premium.PremiumUntil,
            TierName = tier?.Name,
            TierProgress = tier?.Progress ?? 0m,

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
