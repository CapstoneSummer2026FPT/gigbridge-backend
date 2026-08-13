using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Freelancer.GetRecommendedJobPosts.Queries;

public sealed record RecommendedSkill(Guid SkillId, string Name);

public sealed record RecommendedVerifiedWork(
    Guid ContractId,
    string Title,
    string? Description,
    string? MajorName,
    string? CategoryName,
    IReadOnlyList<RecommendedSkill> Skills);

public sealed record RecommendedFreelancerQuery(
    Guid FreelancerProfileId,
    string? Title,
    string? Bio,
    string? Location,
    int Availability,
    Guid? MajorId,
    string? MajorName,
    IReadOnlyList<string> CategoryNames,
    IReadOnlyList<RecommendedSkill> Skills,
    IReadOnlyList<RecommendedVerifiedWork> VerifiedWork);

public sealed record RecommendedJobPool(
    RecommendedFreelancerQuery Freelancer,
    IReadOnlyList<JobPost> Candidates);

public static class RecommendedJobPoolLoader
{
    public const int MaximumCandidatePoolSize = 300;

    public static async Task<RecommendedJobPool> LoadAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var freelancer = await context.Set<FreelancerProfile>()
            .AsNoTracking()
            .AsSplitQuery()
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
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (freelancer is null)
        {
            throw new NotFoundException("Freelancer profile not found.");
        }

        var categoryNames = freelancer.FreelancerProfileCategories
            .Where(selection => selection.MajorCategory?.Category is not null)
            .Select(selection => selection.MajorCategory.Category.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var skills = freelancer.FreelancerSkills
            .Where(selection => selection.Skills is not null)
            .Select(selection => new RecommendedSkill(selection.SkillsId, selection.Skills.Name))
            .DistinctBy(skill => skill.SkillId)
            .OrderBy(skill => skill.Name)
            .ToList();

        var verifiedWork = freelancer.Contracts
            .Where(contract => contract.Status == (int)ContractStatus.Completed)
            .OrderByDescending(contract => contract.CompletedAt)
            .ThenByDescending(contract => contract.UpdatedAt)
            .Take(5)
            .Select(contract =>
            {
                var contractJob = contract.JobPosts;
                var workSkills = contractJob.JobPostSkills
                    .Where(selection => selection.Skills is not null)
                    .Select(selection => new RecommendedSkill(selection.SkillsId, selection.Skills.Name))
                    .DistinctBy(skill => skill.SkillId)
                    .ToList();
                return new RecommendedVerifiedWork(
                    contract.ContractsId,
                    contract.Title,
                    contract.Description,
                    contractJob.MajorCategory?.Major?.Name,
                    contractJob.MajorCategory?.Category?.Name,
                    workSkills);
            })
            .ToList();

        var freelancerQuery = new RecommendedFreelancerQuery(
            freelancer.FreelancerProfilesId,
            freelancer.Title,
            freelancer.Bio,
            freelancer.Location,
            freelancer.Availability ?? 0,
            freelancer.MajorId,
            freelancer.Major?.Name,
            categoryNames,
            skills,
            verifiedWork);

        var appliedJobIds = await context.Set<Proposal>()
            .AsNoTracking()
            .Where(proposal => proposal.FreelancerProfilesId == freelancer.FreelancerProfilesId)
            .Select(proposal => proposal.JobPostsId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var candidates = await context.Set<JobPost>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(jobPost => jobPost.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile.User)
                    .ThenInclude(user => user.UserEloScore)
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Major)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Category)
            .Where(jobPost =>
                jobPost.Status == 1 &&
                (jobPost.Visibility == null || jobPost.Visibility == 0) &&
                !appliedJobIds.Contains(jobPost.JobPostsId))
            .OrderByDescending(jobPost =>
                freelancer.MajorId != null &&
                jobPost.MajorCategory != null &&
                jobPost.MajorCategory.MajorId == freelancer.MajorId)
            .ThenByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
            .ThenByDescending(jobPost => jobPost.CreatedAt)
            .Take(MaximumCandidatePoolSize)
            .ToListAsync(cancellationToken);

        return new RecommendedJobPool(freelancerQuery, candidates);
    }
}
