using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Common;

internal static class JobPostSetupProgressBuilder
{
    private const int DraftJobPostStatus = 0;
    private const int OpenJobPostStatus = 1;
    private const string DefaultDraftTitle = "Untitled Job Post";

    public static async Task ApplyAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<GetMyJobPostDto> jobPosts,
        CancellationToken cancellationToken)
    {
        if (jobPosts.Count == 0)
        {
            return;
        }

        var progressByJobPostId = await BuildAsync(
            context,
            jobPosts.Select(jobPost => new JobPostSetupSource(
                jobPost.JobPostsId,
                jobPost.Title,
                jobPost.Description,
                jobPost.MajorCategoryId,
                jobPost.Status)),
            cancellationToken);

        foreach (var jobPost in jobPosts)
        {
            jobPost.SetupProgress = progressByJobPostId[jobPost.JobPostsId];
        }
    }

    public static async Task ApplyAsync(
        IApplicationDbContext context,
        GetMyJobPostDetailDto jobPost,
        CancellationToken cancellationToken)
    {
        var progressByJobPostId = await BuildAsync(
            context,
            new[]
            {
                new JobPostSetupSource(
                    jobPost.JobPostsId,
                    jobPost.Title,
                    jobPost.Description,
                    jobPost.MajorCategoryId,
                    jobPost.Status)
            },
            cancellationToken);

        jobPost.SetupProgress = progressByJobPostId[jobPost.JobPostsId];
    }

    private static async Task<Dictionary<Guid, JobPostSetupProgressDto>> BuildAsync(
        IApplicationDbContext context,
        IEnumerable<JobPostSetupSource> sources,
        CancellationToken cancellationToken)
    {
        var sourceList = sources.ToList();

        var jobPostIds = sourceList.Select(source => source.JobPostId).ToArray();
        var plans = await context.Set<Domain.Entities.JobPostMilestonePlan>()
            .AsNoTracking()
            .Include(plan => plan.WorkItems)
            .Where(plan => jobPostIds.Contains(plan.JobPostsId))
            .ToListAsync(cancellationToken);

        return sourceList.ToDictionary(
            source => source.JobPostId,
            source => BuildProgress(source, plans.Where(plan => plan.JobPostsId == source.JobPostId).ToList()));
    }

    private static JobPostSetupProgressDto BuildProgress(
        JobPostSetupSource source,
        IReadOnlyCollection<Domain.Entities.JobPostMilestonePlan> milestones)
    {
        var isDetailsComplete = IsDetailsComplete(source);
        var isMilestonePlanComplete = milestones.Count == 0 || milestones.All(milestone =>
            !string.IsNullOrWhiteSpace(milestone.Title) &&
            milestone.Amount > 0 &&
            !string.IsNullOrWhiteSpace(milestone.EstimatedDuration) &&
            !string.IsNullOrWhiteSpace(milestone.Deliverables) &&
            !string.IsNullOrWhiteSpace(milestone.AcceptanceCriteria) &&
            milestone.WorkItems.All(item =>
                !string.IsNullOrWhiteSpace(item.Title) &&
                !string.IsNullOrWhiteSpace(item.Description)));
        var canPublish = source.Status == DraftJobPostStatus &&
            isDetailsComplete &&
            isMilestonePlanComplete;

        return new JobPostSetupProgressDto
        {
            IsDetailsComplete = isDetailsComplete,
            ContractId = null,
            ESignDocumentId = null,
            ESignStatus = null,
            HasMilestones = milestones.Count > 0,
            IsMilestonePlanComplete = isMilestonePlanComplete,
            CanPublish = canPublish,
            NextIncompleteStep = GetNextStep(
                source.Status,
                isDetailsComplete,
                isMilestonePlanComplete,
                canPublish)
        };
    }

    private static string GetNextStep(
        int jobPostStatus,
        bool isDetailsComplete,
        bool isMilestonePlanComplete,
        bool canPublish)
    {
        if (jobPostStatus == OpenJobPostStatus)
        {
            return JobPostSetupStepNames.Published;
        }

        if (!isDetailsComplete)
        {
            return JobPostSetupStepNames.Details;
        }

        if (!isMilestonePlanComplete)
        {
            return JobPostSetupStepNames.Milestones;
        }

        return canPublish
            ? JobPostSetupStepNames.ReadyToPublish
            : JobPostSetupStepNames.Published;
    }

    private static bool IsDetailsComplete(JobPostSetupSource source)
    {
        return source.Status != OpenJobPostStatus
            ? !string.IsNullOrWhiteSpace(source.Title) &&
              !string.Equals(source.Title.Trim(), DefaultDraftTitle, StringComparison.OrdinalIgnoreCase) &&
              !string.IsNullOrWhiteSpace(source.Description) &&
              source.MajorCategoryId.HasValue
            : true;
    }

    private sealed record JobPostSetupSource(
        Guid JobPostId,
        string Title,
        string Description,
        Guid? MajorCategoryId,
        int Status);
}
