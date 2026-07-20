using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed record TalentCandidateItem(
    TalentScoredCandidate Scored,
    TalentScoringCandidate Candidate);

public sealed record TalentMatchingShortlist(
    TalentScoringJob Job,
    string JobTitle,
    string? JobDescription,
    IReadOnlyList<TalentCandidateItem> Candidates);

public static class TalentMatchingCandidateLoader
{
    public static async Task<TalentMatchingShortlist> LoadAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid jobPostId,
        DateTime utcNow,
        int shortlistSize,
        TalentMatchingFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>()
            .AsNoTracking()
            .Include(j => j.JobPostSkills)
                .ThenInclude(js => js.Skills)
            .Include(j => j.MajorCategory)
            .FirstOrDefaultAsync(j => j.JobPostsId == jobPostId && j.ClientProfiles.UserId == userId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post not found.");
        }

        var jobSkills = jobPost.JobPostSkills
            .Where(js => js.Skills is not null)
            .Select(js => new TalentScoringSkill(js.SkillsId, js.Skills!.Name, js.IsRequired ?? false))
            .ToList();

        var customSkills = jobPost.CustomSkillNames?.ToList() ?? new List<string>();

        var scoringJob = new TalentScoringJob(
            jobPost.JobPostsId,
            jobPost.MajorCategoryId,
            jobPost.MajorCategory?.MajorId,
            jobSkills,
            customSkills);

        var freelancers = await context.Set<FreelancerProfile>()
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FreelancerSkills)
                .ThenInclude(fs => fs.Skills)
            .Include(f => f.FreelancerProfileCategories)
            .Include(f => f.WorkExperiences)
            .Include(f => f.Contracts)
            .Where(f => f.User.IsActive && (!f.Availability.HasValue || f.Availability.Value == 0 || f.Availability.Value == 1))
            .ToListAsync(cancellationToken);

        var items = new List<TalentCandidateItem>();

        foreach (var f in freelancers)
        {
            var skills = f.FreelancerSkills
                .Where(fs => fs.Skills is not null)
                .Select(fs => new TalentScoringSkill(fs.SkillsId, fs.Skills!.Name, false, (int)fs.ProficiencyLevel, fs.YearsOfExperience))
                .ToList();

            var workExps = f.WorkExperiences
                .Select(we => new TalentScoringWorkExperience(we.Title, we.CompanyName, we.Description))
                .ToList();

            var verifiedContracts = f.Contracts
                .Where(c => c.Status == 4) // Completed
                .Select(c => new TalentVerifiedContractEvidence(c.ContractsId, null, null, new HashSet<Guid>()))
                .ToList();

            var majorCategoryIds = f.FreelancerProfileCategories
                .Select(fc => fc.MajorCategoryId)
                .ToHashSet();

            var candidate = new TalentScoringCandidate(
                f.FreelancerProfilesId,
                f.UserId,
                f.User.FullName ?? "Freelancer",
                f.Title,
                f.Bio,
                f.Availability ?? 0,
                f.ProfileCompletionScore ?? 100,
                0, // EloPoints
                5.0, // AverageRating
                0, // ReviewCount
                verifiedContracts.Count, // CompletedContractCount
                f.MajorId,
                majorCategoryIds,
                skills,
                workExps,
                verifiedContracts);

            var scored = TalentMatchScorer.Score(scoringJob, candidate);
            if (scored is not null)
            {
                items.Add(new TalentCandidateItem(scored, candidate));
            }
        }

        var sorted = items
            .OrderByDescending(i => i.Scored.Match.MatchPercentage)
            .Take(shortlistSize)
            .ToList();

        return new TalentMatchingShortlist(scoringJob, jobPost.Title, jobPost.Description, sorted);
    }
}
