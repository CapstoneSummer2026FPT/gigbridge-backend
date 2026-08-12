using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.Models;
using Application.Features.Premium.Common;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancers.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Premium;
using Domain.Enums.Reviews;
using Domain.Enums.Subscriptions;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancers.Queries;

public sealed class GetFreelancersQueryHandler
    : IRequestHandler<GetFreelancersQuery, PaginatedList<FreelancerSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public GetFreelancersQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<PaginatedList<FreelancerSummaryDto>> Handle(
        GetFreelancersQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var now = _clock.UtcNow;

        var reviews = _context.Set<Review>()
            .AsNoTracking()
            .Where(review => review.ModerationStatus == (int)ReviewModerationStatus.Active);
        var promotions = _context.Set<FreelancerProfilePromotion>().AsNoTracking();
        var subscriptions = _context.Set<Subscription>().AsNoTracking();

        IQueryable<Domain.Entities.FreelancerProfile> profiles =
            _context.Set<Domain.Entities.FreelancerProfile>()
                .AsNoTracking()
                .Where(profile => profile.User.IsActive);

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            profiles = profiles.Where(profile =>
                profile.User.FullName.ToLower().Contains(search) ||
                (profile.Title != null && profile.Title.ToLower().Contains(search)) ||
                (profile.Bio != null && profile.Bio.ToLower().Contains(search)) ||
                (profile.Location != null && profile.Location.ToLower().Contains(search)) ||
                (profile.Major != null && profile.Major.Name.ToLower().Contains(search)) ||
                profile.FreelancerSkills.Any(skill => skill.Skills.Name.ToLower().Contains(search)) ||
                profile.FreelancerProfileCategories.Any(selection =>
                    selection.MajorCategory.Category.Name.ToLower().Contains(search)));
        }

        var skillNames = request.Skills?
            .Select(skill => skill.Trim().ToLowerInvariant())
            .Where(skill => skill.Length > 0)
            .Distinct()
            .ToList();
        if (skillNames is { Count: > 0 })
        {
            profiles = profiles.Where(profile =>
                profile.FreelancerSkills.Any(skill =>
                    skillNames.Contains(skill.Skills.Name.ToLower())));
        }

        profiles = ApplyAvailabilityFilter(profiles, request.AvailabilityStatus);

        if (request.MinRating.HasValue)
        {
            var minimumRating = request.MinRating.Value;
            profiles = profiles.Where(profile =>
                (reviews
                    .Where(review => review.RevieweeId == profile.UserId)
                    .Select(review => (double?)review.Rating)
                    .Average() ?? 0d) >= minimumRating);
        }

        var totalCount = await profiles.CountAsync(cancellationToken);

        var rows = profiles.Select(profile => new FreelancerSummaryRow
        {
            FreelancerProfilesId = profile.FreelancerProfilesId,
            UserId = profile.UserId,
            UserFullName = profile.User.FullName,
            UserAvatar = profile.User.Avatar,
            Title = profile.Title,
            Bio = profile.Bio,
            Availability = profile.Availability,
            Location = profile.Location,
            MajorId = profile.MajorId,
            MajorName = profile.Major != null ? profile.Major.Name : null,
            Rating = reviews
                .Where(review => review.RevieweeId == profile.UserId)
                .Select(review => (double?)review.Rating)
                .Average() ?? 0d,
            EloPoints = profile.User.UserEloScore != null
                ? profile.User.UserEloScore.CurrentPoints
                : UserEloCalculator.DefaultPoints,
            PromotionBoost = promotions
                .Where(promotion =>
                    promotion.FreelancerProfileId == profile.FreelancerProfilesId &&
                    promotion.Status == PromotionStatus.Active &&
                    promotion.StartTime <= now &&
                    promotion.EndTime > now)
                .Select(promotion => (decimal?)promotion.BoostWeight)
                .Max() ?? 0m,
            PremiumUntil = subscriptions
                .Where(subscription =>
                    subscription.UserId == profile.UserId &&
                    subscription.Status == SubscriptionStatus.Active &&
                    subscription.StartDate <= now &&
                    subscription.EndDate > now &&
                    subscription.SubscriptionPlans.IsActive == true &&
                    subscription.SubscriptionPlans.Price > 0 &&
                    (subscription.SubscriptionPlans.TargetRole == null ||
                     subscription.SubscriptionPlans.TargetRole == (int)UserRole.Freelancer))
                .Select(subscription => (DateTime?)subscription.EndDate)
                .Max(),
            IsIdentityVerified = profile.User.IsEmailVerified,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        });

        rows = ApplySort(rows, request.Sort);

        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return new PaginatedList<FreelancerSummaryDto>([], totalCount, page, pageSize);
        }

        var profileIds = pageRows.Select(row => row.FreelancerProfilesId).ToList();

        var skillRows = await _context.Set<FreelancerSkill>()
            .AsNoTracking()
            .Where(skill => profileIds.Contains(skill.FreelancerId))
            .OrderBy(skill => skill.Skills.Name)
            .ThenBy(skill => skill.SkillsId)
            .Select(skill => new FreelancerSkillRow
            {
                FreelancerProfileId = skill.FreelancerId,
                SkillId = skill.SkillsId,
                SkillName = skill.Skills.Name,
                ProficiencyLevel = skill.ProficiencyLevel
            })
            .ToListAsync(cancellationToken);

        var categoryRows = await _context.Set<FreelancerProfileCategory>()
            .AsNoTracking()
            .Where(selection => profileIds.Contains(selection.FreelancerProfileId))
            .OrderBy(selection => selection.MajorCategory.Category.SortOrder)
            .ThenBy(selection => selection.MajorCategory.Category.Name)
            .ThenBy(selection => selection.MajorCategoryId)
            .Select(selection => new FreelancerCategoryRow
            {
                FreelancerProfileId = selection.FreelancerProfileId,
                MajorCategoryId = selection.MajorCategoryId,
                CategoryId = selection.MajorCategory.CategoryId,
                Name = selection.MajorCategory.Category.Name
            })
            .ToListAsync(cancellationToken);

        var tierSetting = await _context.Set<PlatformSetting>()
            .AsNoTracking()
            .Where(setting => setting.Key == PremiumTierCalculator.SettingKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var skillsByProfile = skillRows
            .GroupBy(skill => skill.FreelancerProfileId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(skill => new FreelancerSkillDto
                {
                    SkillId = skill.SkillId,
                    SkillName = skill.SkillName,
                    ProficiencyLevel = skill.ProficiencyLevel
                }).ToList());
        var categoriesByProfile = categoryRows
            .GroupBy(category => category.FreelancerProfileId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(category => new FreelancerProfileCategoryDto
                {
                    MajorCategoryId = category.MajorCategoryId,
                    CategoryId = category.CategoryId,
                    Name = category.Name
                }).ToList());

        var items = pageRows.Select(row =>
        {
            var isPremium = row.PremiumUntil.HasValue;
            var tier = isPremium
                ? PremiumTierCalculator.Calculate(row.EloPoints, tierSetting)
                : null;

            return new FreelancerSummaryDto
            {
                FreelancerProfilesId = row.FreelancerProfilesId,
                UserId = row.UserId,
                UserFullName = row.UserFullName,
                UserAvatar = row.UserAvatar,
                Title = row.Title,
                Bio = row.Bio,
                Availability = row.Availability,
                Location = row.Location,
                MajorId = row.MajorId,
                MajorName = row.MajorName,
                Rating = Math.Round(row.Rating, 1),
                EloPoints = row.EloPoints,
                IsPremium = isPremium,
                IsIdentityVerified = row.IsIdentityVerified,
                ShowProVerifiedBadge = isPremium && row.IsIdentityVerified,
                PremiumUntil = row.PremiumUntil,
                TierName = tier?.Name,
                TierProgress = tier?.Progress ?? 0m,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                Skills = skillsByProfile.GetValueOrDefault(row.FreelancerProfilesId) ?? [],
                Categories = categoriesByProfile.GetValueOrDefault(row.FreelancerProfilesId) ?? []
            };
        }).ToList();

        return new PaginatedList<FreelancerSummaryDto>(items, totalCount, page, pageSize);
    }

    private static IQueryable<Domain.Entities.FreelancerProfile> ApplyAvailabilityFilter(
        IQueryable<Domain.Entities.FreelancerProfile> profiles,
        string? availabilityStatus)
    {
        return availabilityStatus?.Trim().ToLowerInvariant() switch
        {
            "available" => profiles.Where(profile =>
                profile.Availability == 0 || profile.Availability == 1),
            "busy" or "parttime" or "1" => profiles.Where(profile => profile.Availability == 1),
            "fulltime" or "0" => profiles.Where(profile => profile.Availability == 0),
            "notavailable" or "2" => profiles.Where(profile => profile.Availability == 2),
            _ => profiles
        };
    }

    private static IQueryable<FreelancerSummaryRow> ApplySort(
        IQueryable<FreelancerSummaryRow> rows,
        string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "rating" => rows
                .OrderByDescending(row => row.Rating)
                .ThenByDescending(row => row.EloPoints)
                .ThenByDescending(row => row.CreatedAt)
                .ThenBy(row => row.FreelancerProfilesId),
            "elo" => rows
                .OrderByDescending(row => row.EloPoints)
                .ThenByDescending(row => row.Rating)
                .ThenByDescending(row => row.CreatedAt)
                .ThenBy(row => row.FreelancerProfilesId),
            "newest" => rows
                .OrderByDescending(row => row.CreatedAt)
                .ThenBy(row => row.FreelancerProfilesId),
            _ => rows
                .OrderByDescending(row => row.PromotionBoost)
                .ThenByDescending(row => row.EloPoints)
                .ThenByDescending(row => row.CreatedAt)
                .ThenBy(row => row.FreelancerProfilesId)
        };
    }

    private sealed class FreelancerSummaryRow
    {
        public Guid FreelancerProfilesId { get; init; }
        public Guid UserId { get; init; }
        public string? UserFullName { get; init; }
        public string? UserAvatar { get; init; }
        public string? Title { get; init; }
        public string? Bio { get; init; }
        public int? Availability { get; init; }
        public string? Location { get; init; }
        public Guid? MajorId { get; init; }
        public string? MajorName { get; init; }
        public double Rating { get; init; }
        public int EloPoints { get; init; }
        public decimal PromotionBoost { get; init; }
        public DateTime? PremiumUntil { get; init; }
        public bool IsIdentityVerified { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed class FreelancerSkillRow
    {
        public Guid FreelancerProfileId { get; init; }
        public Guid SkillId { get; init; }
        public string SkillName { get; init; } = string.Empty;
        public int? ProficiencyLevel { get; init; }
    }

    private sealed class FreelancerCategoryRow
    {
        public Guid FreelancerProfileId { get; init; }
        public Guid MajorCategoryId { get; init; }
        public Guid CategoryId { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
