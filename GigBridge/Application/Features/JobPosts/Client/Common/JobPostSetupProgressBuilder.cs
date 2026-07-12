using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;

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

    private static Task<Dictionary<Guid, JobPostSetupProgressDto>> BuildAsync(
        IApplicationDbContext context,
        IEnumerable<JobPostSetupSource> sources,
        CancellationToken cancellationToken)
    {
        var sourceList = sources.ToList();

        var progressByJobPostId = sourceList.ToDictionary(
            source => source.JobPostId,
            BuildProgress);

        return Task.FromResult(progressByJobPostId);
    }

    private static JobPostSetupProgressDto BuildProgress(JobPostSetupSource source)
    {
        var isDetailsComplete = IsDetailsComplete(source);
        var canPublish = source.Status == DraftJobPostStatus &&
            isDetailsComplete;

        return new JobPostSetupProgressDto
        {
            IsDetailsComplete = isDetailsComplete,
            ContractId = null,
            ESignDocumentId = null,
            ESignStatus = null,
            HasMilestones = false,
            CanPublish = canPublish,
            NextIncompleteStep = GetNextStep(
                source.Status,
                isDetailsComplete,
                canPublish)
        };
    }

    private static string GetNextStep(
        int jobPostStatus,
        bool isDetailsComplete,
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
