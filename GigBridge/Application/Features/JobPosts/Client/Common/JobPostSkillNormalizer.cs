using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Common;

internal sealed record NormalizedJobPostSkills(
    IReadOnlyList<Guid> SkillIds,
    string[] CustomSkillNames
);

internal static class JobPostSkillNormalizer
{
    public static async Task<NormalizedJobPostSkills> NormalizeAsync(
        IApplicationDbContext context,
        Guid? majorCategoryId,
        List<Guid>? skillIds,
        List<string>? customSkillNames,
        CancellationToken cancellationToken)
    {
        var categoryId = await ResolveCategoryId(context, majorCategoryId, cancellationToken);

        var finalSkillIds = (skillIds ?? new List<Guid>())
            .Distinct()
            .ToList();

        var finalCustomSkillNames = (customSkillNames ?? new List<string>())
            .Where(skillName => !string.IsNullOrWhiteSpace(skillName))
            .Select(skillName => skillName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (categoryId.HasValue && finalCustomSkillNames.Count > 0)
        {
            var officialSkills = await context.Set<CategorySkill>()
                .AsNoTracking()
                .Where(categorySkill =>
                    categorySkill.CategoryId == categoryId.Value &&
                    categorySkill.Category.IsActive &&
                    categorySkill.Skill.IsActive)
                .Select(categorySkill => new
                {
                    categorySkill.SkillId,
                    categorySkill.Skill.Name
                })
                .ToListAsync(cancellationToken);

            var officialSkillsByName = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var officialSkill in officialSkills)
            {
                var canonicalKey = CanonicalSkillKey(officialSkill.Name);
                if (canonicalKey.Length > 0 && !officialSkillsByName.ContainsKey(canonicalKey))
                {
                    officialSkillsByName.Add(canonicalKey, officialSkill.SkillId);
                }
            }

            var unmatchedCustomSkillNames = new List<string>();
            foreach (var customSkillName in finalCustomSkillNames)
            {
                if (officialSkillsByName.TryGetValue(CanonicalSkillKey(customSkillName), out var officialSkillId))
                {
                    finalSkillIds.Add(officialSkillId);
                }
                else
                {
                    unmatchedCustomSkillNames.Add(customSkillName);
                }
            }

            finalCustomSkillNames = unmatchedCustomSkillNames;
        }

        finalSkillIds = finalSkillIds
            .Distinct()
            .ToList();

        await ValidateSkillIds(context, finalSkillIds, cancellationToken);

        return new NormalizedJobPostSkills(
            finalSkillIds,
            finalCustomSkillNames.ToArray());
    }

    private static async Task<Guid?> ResolveCategoryId(
        IApplicationDbContext context,
        Guid? majorCategoryId,
        CancellationToken cancellationToken)
    {
        if (!majorCategoryId.HasValue)
        {
            return null;
        }

        var categoryId = await context.Set<MajorCategory>()
            .AsNoTracking()
            .Where(majorCategory => majorCategory.MajorCategoriesId == majorCategoryId.Value)
            .Select(majorCategory => (Guid?)majorCategory.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!categoryId.HasValue)
        {
            throw new NotFoundException("Major category does not exist.");
        }

        return categoryId;
    }

    private static async Task ValidateSkillIds(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        if (skillIds.Count == 0)
        {
            return;
        }

        var existingSkillCount = await context.Set<Skill>()
            .CountAsync(skill => skillIds.Contains(skill.SkillsId), cancellationToken);

        if (existingSkillCount != skillIds.Count)
        {
            throw new NotFoundException("One or more skills do not exist.");
        }
    }

    private static string CanonicalSkillKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var expanded = value.Trim().ToLowerInvariant()
            .Replace("#", "sharp", StringComparison.Ordinal)
            .Replace("+", "plus", StringComparison.Ordinal)
            .Replace("&", "and", StringComparison.Ordinal);
        return new string(expanded.Where(char.IsLetterOrDigit).ToArray());
    }
}
