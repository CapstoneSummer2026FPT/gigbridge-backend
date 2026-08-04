using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.FreelancerProfile.Common;

internal static class FreelancerProfileSkills
{
    public static async Task<IReadOnlyList<Skill>> ValidateAndLoadAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        var distinctSkillIds = skillIds.Distinct().ToArray();
        if (distinctSkillIds.Length != skillIds.Count)
        {
            throw new BadRequestException("Duplicate skills are not allowed.");
        }

        var skills = await context.Set<Skill>()
            .Where(skill => distinctSkillIds.Contains(skill.SkillsId) && skill.IsActive)
            .OrderBy(skill => skill.Name)
            .ToListAsync(cancellationToken);
        if (skills.Count != distinctSkillIds.Length)
        {
            throw new BadRequestException("Every selected skill must exist and be active.");
        }

        return skills;
    }

    public static async Task SynchronizeAsync(
        IApplicationDbContext context,
        Domain.Entities.FreelancerProfile profile,
        IReadOnlyList<Skill> skills,
        CancellationToken cancellationToken)
    {
        var requestedSkillIds = skills.Select(skill => skill.SkillsId).ToHashSet();
        var existingSelections = await context.Set<FreelancerSkill>()
            .Include(selection => selection.Skills)
            .Where(selection => selection.FreelancerId == profile.FreelancerProfilesId)
            .ToListAsync(cancellationToken);
        var selectionsToRemove = existingSelections
            .Where(selection => !requestedSkillIds.Contains(selection.SkillsId))
            .ToList();
        if (selectionsToRemove.Count > 0)
        {
            context.Set<FreelancerSkill>().RemoveRange(selectionsToRemove);
            foreach (var selection in selectionsToRemove)
            {
                profile.FreelancerSkills.Remove(selection);
            }
        }

        var existingSkillIdSet = existingSelections
            .Select(selection => selection.SkillsId)
            .ToHashSet();
        foreach (var skill in skills.Where(skill => !existingSkillIdSet.Contains(skill.SkillsId)))
        {
            var selection = new FreelancerSkill
            {
                FreelancerSkillsId = Guid.NewGuid(),
                FreelancerId = profile.FreelancerProfilesId,
                SkillsId = skill.SkillsId,
                Skills = skill
            };
            profile.FreelancerSkills.Add(selection);
            context.Set<FreelancerSkill>().Add(selection);
        }
    }
}
