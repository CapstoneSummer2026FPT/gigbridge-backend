using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed record AiTalentSkill(Guid SkillId, string Name);

public sealed record AiVerifiedWork(
    Guid ContractId,
    string Title,
    string? Description,
    Guid? MajorId,
    string? MajorName,
    Guid? MajorCategoryId,
    string? CategoryName,
    IReadOnlyList<AiTalentSkill> Skills);

public sealed record AiTalentMatchingJob(
    Guid JobPostId,
    string Title,
    string? Description,
    string? ClientIndustry,
    Guid? MajorId,
    string? MajorName,
    Guid? MajorCategoryId,
    string? CategoryName,
    IReadOnlyList<AiTalentSkill> Skills,
    IReadOnlyList<string> CustomSkills,
    string? Location,
    string? EstimatedDuration);

public sealed record AiTalentMatchingCandidate(
    Guid FreelancerProfileId,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string? Title,
    string? Bio,
    string? Location,
    int Availability,
    Guid? MajorId,
    string? MajorName,
    IReadOnlySet<Guid> MajorCategoryIds,
    IReadOnlyList<string> CategoryNames,
    IReadOnlyList<AiTalentSkill> Skills,
    int CompletedContractCount,
    IReadOnlyList<AiVerifiedWork> VerifiedWork,
    int EloPoints,
    double AverageRating,
    int ReviewCount);

public sealed record AiTalentMatchingPool(
    AiTalentMatchingJob Job,
    IReadOnlyList<AiTalentMatchingCandidate> Candidates);

public static class TalentMatchingCandidateLoader
{
    public const int MaximumCandidatePoolSize = 500;

    public static async Task<AiTalentMatchingPool> LoadAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid jobPostId,
        TalentMatchingFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>()
            .AsNoTracking()
            .Include(job => job.ClientProfiles)
            .Include(job => job.JobPostSkills)
                .ThenInclude(selection => selection.Skills)
            .Include(job => job.MajorCategory)
                .ThenInclude(mapping => mapping!.Major)
            .Include(job => job.MajorCategory)
                .ThenInclude(mapping => mapping!.Category)
            .FirstOrDefaultAsync(job =>
                    job.JobPostsId == jobPostId &&
                    job.Status == 1 &&
                    job.ClientProfiles.UserId == userId,
                cancellationToken);
        if (jobPost is null)
        {
            throw new NotFoundException("Open job post not found.");
        }

        var jobSkills = jobPost.JobPostSkills
            .Where(selection => selection.Skills is not null)
            .Select(selection => new AiTalentSkill(selection.SkillsId, selection.Skills.Name))
            .DistinctBy(skill => skill.SkillId)
            .OrderBy(skill => skill.Name)
            .ToList();
        var job = new AiTalentMatchingJob(
            jobPost.JobPostsId,
            jobPost.Title,
            jobPost.Description,
            jobPost.ClientProfiles.Industry,
            jobPost.MajorCategory?.MajorId,
            jobPost.MajorCategory?.Major?.Name,
            jobPost.MajorCategoryId,
            jobPost.MajorCategory?.Category?.Name,
            jobSkills,
            (jobPost.CustomSkillNames ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            jobPost.Location,
            jobPost.EstimatedDuration);

        var eligibleQuery = context.Set<FreelancerProfile>()
            .AsNoTracking()
            .Where(profile =>
                profile.User.IsActive &&
                profile.Availability.HasValue &&
                (profile.Availability.Value == 0 || profile.Availability.Value == 1));

        if (filters?.Availability is { } availability)
        {
            eligibleQuery = eligibleQuery.Where(profile => profile.Availability == availability);
        }
        if (filters?.MajorId is { } majorId)
        {
            eligibleQuery = eligibleQuery.Where(profile => profile.MajorId == majorId);
        }
        if (filters?.MajorCategoryId is { } majorCategoryId)
        {
            eligibleQuery = eligibleQuery.Where(profile => profile.FreelancerProfileCategories
                .Any(selection => selection.MajorCategoryId == majorCategoryId));
        }
        if (filters?.SkillIds is { Count: > 0 })
        {
            var requestedSkillIds = filters.SkillIds.Distinct().ToArray();
            eligibleQuery = eligibleQuery.Where(profile => profile.FreelancerSkills
                .Count(selection => requestedSkillIds.Contains(selection.SkillsId)) == requestedSkillIds.Length);
        }
        if (filters?.MinimumCompletedContracts is { } minimumCompletedContracts)
        {
            eligibleQuery = eligibleQuery.Where(profile => profile.Contracts
                .Count(contract => contract.Status == (int)ContractStatus.Completed) >= minimumCompletedContracts);
        }
        if (filters?.MinimumProficiency.HasValue == true || filters?.MinimumYears.HasValue == true)
        {
            eligibleQuery = eligibleQuery.Where(profile => profile.FreelancerSkills.Any(selection =>
                (!filters!.MinimumProficiency.HasValue ||
                    selection.ProficiencyLevel >= filters.MinimumProficiency.Value) &&
                (!filters.MinimumYears.HasValue ||
                    selection.YearsOfExperience >= filters.MinimumYears.Value)));
        }
        if (filters?.MinimumRating is { } minimumRating)
        {
            eligibleQuery = eligibleQuery.Where(profile => context.Set<Review>()
                .Where(review =>
                    review.RevieweeId == profile.UserId &&
                    review.ModerationStatus == (int)ReviewModerationStatus.Active)
                .Average(review => (double?)review.Rating) >= minimumRating);
        }

        var jobSkillIds = job.Skills.Select(skill => skill.SkillId).ToArray();
        var shortlistedProfileIds = await eligibleQuery
            .Select(profile => new
            {
                ProfileId = profile.FreelancerProfilesId,
                DeclaredSkillMatches = profile.FreelancerSkills
                    .Count(selection => jobSkillIds.Contains(selection.SkillsId)),
                VerifiedSkillMatches = profile.Contracts.Count(contract =>
                    contract.Status == (int)ContractStatus.Completed &&
                    contract.JobPosts.JobPostSkills.Any(selection =>
                        jobSkillIds.Contains(selection.SkillsId))),
                ExactCategoryMatch = job.MajorCategoryId.HasValue &&
                    (profile.FreelancerProfileCategories.Any(selection =>
                         selection.MajorCategoryId == job.MajorCategoryId.Value) ||
                     profile.Contracts.Any(contract =>
                         contract.Status == (int)ContractStatus.Completed &&
                         contract.JobPosts.MajorCategoryId == job.MajorCategoryId.Value)),
                SameMajorMatch = job.MajorId.HasValue &&
                    (profile.MajorId == job.MajorId.Value ||
                     profile.Contracts.Any(contract =>
                         contract.Status == (int)ContractStatus.Completed &&
                         contract.JobPosts.MajorCategory != null &&
                         contract.JobPosts.MajorCategory.MajorId == job.MajorId.Value)),
                CompletedContracts = profile.Contracts.Count(contract =>
                    contract.Status == (int)ContractStatus.Completed)
            })
            .OrderByDescending(candidate => candidate.DeclaredSkillMatches)
            .ThenByDescending(candidate => candidate.VerifiedSkillMatches)
            .ThenByDescending(candidate => candidate.ExactCategoryMatch)
            .ThenByDescending(candidate => candidate.SameMajorMatch)
            .ThenByDescending(candidate => candidate.CompletedContracts)
            .ThenBy(candidate => candidate.ProfileId)
            .Take(MaximumCandidatePoolSize)
            .Select(candidate => candidate.ProfileId)
            .ToListAsync(cancellationToken);

        var profiles = await context.Set<FreelancerProfile>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(profile => profile.User)
                .ThenInclude(user => user.UserEloScore)
            .Include(profile => profile.Major)
            .Include(profile => profile.FreelancerSkills)
                .ThenInclude(selection => selection.Skills)
            .Include(profile => profile.FreelancerProfileCategories)
                .ThenInclude(selection => selection.MajorCategory)
                    .ThenInclude(mapping => mapping.Category)
            .Include(profile => profile.Contracts)
                .ThenInclude(contract => contract.JobPosts)
                    .ThenInclude(contractJob => contractJob.JobPostSkills)
                        .ThenInclude(selection => selection.Skills)
            .Include(profile => profile.Contracts)
                .ThenInclude(contract => contract.JobPosts)
                    .ThenInclude(contractJob => contractJob.MajorCategory)
                        .ThenInclude(mapping => mapping!.Major)
            .Include(profile => profile.Contracts)
                .ThenInclude(contract => contract.JobPosts)
                    .ThenInclude(contractJob => contractJob.MajorCategory)
                        .ThenInclude(mapping => mapping!.Category)
            .Where(profile => shortlistedProfileIds.Contains(profile.FreelancerProfilesId))
            .ToListAsync(cancellationToken);
        var userIds = profiles.Select(profile => profile.UserId).ToArray();
        var reviewStats = userIds.Length == 0
            ? new Dictionary<Guid, ReviewAggregate>()
            : await context.Set<Review>()
                .AsNoTracking()
                .Where(review =>
                    userIds.Contains(review.RevieweeId) &&
                    review.ModerationStatus == (int)ReviewModerationStatus.Active)
                .GroupBy(review => review.RevieweeId)
                .Select(group => new ReviewAggregate(group.Key, group.Average(review => (double?)review.Rating) ?? 0, group.Count()))
                .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var candidates = profiles.Select(profile =>
        {
            reviewStats.TryGetValue(profile.UserId, out var reviews);
            var categories = profile.FreelancerProfileCategories
                .Where(selection => selection.MajorCategory?.Category is not null)
                .OrderBy(selection => selection.MajorCategory.Category.Name)
                .ToList();
            var skills = profile.FreelancerSkills
                .Where(selection => selection.Skills is not null)
                .Select(selection => new AiTalentSkill(selection.SkillsId, selection.Skills.Name))
                .DistinctBy(skill => skill.SkillId)
                .OrderBy(skill => skill.Name)
                .ToList();
            var completedContracts = profile.Contracts
                .Where(contract => contract.Status == (int)ContractStatus.Completed)
                .OrderByDescending(contract => contract.CompletedAt)
                .ThenByDescending(contract => contract.UpdatedAt)
                .ToList();
            var verifiedWork = completedContracts
                .Take(5)
                .Select(contract =>
                {
                    var contractJob = contract.JobPosts;
                    var workSkills = contractJob.JobPostSkills
                        .Where(selection => selection.Skills is not null)
                        .Select(selection => new AiTalentSkill(selection.SkillsId, selection.Skills.Name))
                        .DistinctBy(skill => skill.SkillId)
                        .OrderBy(skill => skill.Name)
                        .ToList();
                    return new AiVerifiedWork(
                        contract.ContractsId,
                        contract.Title,
                        contract.Description,
                        contractJob.MajorCategory?.MajorId,
                        contractJob.MajorCategory?.Major?.Name,
                        contractJob.MajorCategoryId,
                        contractJob.MajorCategory?.Category?.Name,
                        workSkills);
                })
                .ToList();

            return new AiTalentMatchingCandidate(
                profile.FreelancerProfilesId,
                profile.UserId,
                profile.User.FullName ?? "Freelancer",
                profile.User.Avatar,
                profile.Title,
                profile.Bio,
                profile.Location,
                profile.Availability!.Value,
                profile.MajorId,
                profile.Major?.Name,
                categories.Select(selection => selection.MajorCategoryId).ToHashSet(),
                categories.Select(selection => selection.MajorCategory.Category.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                skills,
                completedContracts.Count,
                verifiedWork,
                profile.User.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
                reviews?.AverageRating ?? 0d,
                reviews?.ReviewCount ?? 0);
        }).ToList();

        return new AiTalentMatchingPool(job, candidates);
    }

    private sealed record ReviewAggregate(Guid UserId, double AverageRating, int ReviewCount);
}
