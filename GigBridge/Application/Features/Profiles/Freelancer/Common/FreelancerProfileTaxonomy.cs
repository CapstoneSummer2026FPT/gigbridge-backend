using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.FreelancerProfile.Common;

internal static class FreelancerProfileTaxonomy
{
    public static async Task<IReadOnlyList<MajorCategory>> ValidateAndLoadAsync(
        IApplicationDbContext context,
        Guid majorId,
        IReadOnlyCollection<Guid>? categoryIds,
        CancellationToken cancellationToken)
    {
        if (majorId == Guid.Empty)
        {
            throw new BadRequestException("Major is required.");
        }

        if (categoryIds is null || categoryIds.Count == 0)
        {
            throw new BadRequestException("At least one category is required.");
        }

        var distinctCategoryIds = categoryIds.Distinct().ToArray();
        if (distinctCategoryIds.Length != categoryIds.Count)
        {
            throw new BadRequestException("Duplicate categories are not allowed.");
        }

        var majorIsActive = await context.Set<Major>()
            .AsNoTracking()
            .AnyAsync(major => major.MajorsId == majorId && major.IsActive, cancellationToken);
        if (!majorIsActive)
        {
            throw new BadRequestException("The selected major does not exist or is inactive.");
        }

        var mappings = await context.Set<MajorCategory>()
            .Include(mapping => mapping.Major)
            .Include(mapping => mapping.Category)
            .Where(mapping =>
                mapping.MajorId == majorId &&
                mapping.Major.IsActive &&
                mapping.Category.IsActive &&
                distinctCategoryIds.Contains(mapping.CategoryId))
            .ToListAsync(cancellationToken);

        if (mappings.Count != distinctCategoryIds.Length)
        {
            throw new BadRequestException("Every selected category must be active and belong to the selected major.");
        }

        return mappings;
    }

    public static void ReplaceSelections(
        IApplicationDbContext context,
        Domain.Entities.FreelancerProfile profile,
        Guid majorId,
        IReadOnlyList<MajorCategory> mappings,
        DateTime now)
    {
        if (profile.FreelancerProfileCategories.Count > 0)
        {
            context.Set<FreelancerProfileCategory>().RemoveRange(profile.FreelancerProfileCategories);
            profile.FreelancerProfileCategories.Clear();
        }

        profile.MajorId = majorId;
        profile.Major = mappings[0].Major;

        foreach (var mapping in mappings)
        {
            profile.FreelancerProfileCategories.Add(new FreelancerProfileCategory
            {
                FreelancerProfileCategoriesId = Guid.NewGuid(),
                FreelancerProfileId = profile.FreelancerProfilesId,
                MajorCategoryId = mapping.MajorCategoriesId,
                MajorCategory = mapping,
                CreatedAt = now
            });
        }
    }

    public static int CalculateCompletionScore(Domain.Entities.FreelancerProfile profile)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(profile.Title)) score += 20;
        if (!string.IsNullOrWhiteSpace(profile.Bio)) score += 20;
        if (profile.Availability is not null) score += 15;
        if (!string.IsNullOrWhiteSpace(profile.Location)) score += 15;
        if (profile.MajorId is not null) score += 15;
        if (profile.FreelancerProfileCategories.Count > 0) score += 15;
        return score;
    }

    public static bool IsSetupComplete(Domain.Entities.FreelancerProfile profile) =>
        !string.IsNullOrWhiteSpace(profile.Title) &&
        !string.IsNullOrWhiteSpace(profile.Bio) &&
        !string.IsNullOrWhiteSpace(profile.Location) &&
        profile.MajorId is not null &&
        profile.FreelancerProfileCategories.Count > 0;
}
