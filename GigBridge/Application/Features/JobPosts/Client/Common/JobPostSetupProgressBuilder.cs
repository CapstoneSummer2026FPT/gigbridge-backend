using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using Domain.Entities;
using Domain.Enums;
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

        var contracts = await context.Set<Contract>()
            .AsNoTracking()
            .Where(contract => jobPostIds.Contains(contract.JobPostsId))
            .Select(contract => new
            {
                contract.JobPostsId,
                contract.ContractsId
            })
            .ToListAsync(cancellationToken);

        var contractByJobPostId = contracts
            .GroupBy(contract => contract.JobPostsId)
            .ToDictionary(group => group.Key, group => group.First());

        var contractIds = contracts
            .Select(contract => contract.ContractsId)
            .ToArray();

        var documents = await context.Set<EsignDocument>()
            .AsNoTracking()
            .Where(document =>
                jobPostIds.Contains(document.JobPostsId) &&
                document.ContractsId == null)
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new
            {
                document.JobPostsId,
                document.EsignDocumentsId,
                document.Status
            })
            .ToListAsync(cancellationToken);

        var documentByJobPostId = documents
            .GroupBy(document => document.JobPostsId)
            .ToDictionary(group => group.Key, group => group.First());

        var milestones = await context.Set<Milestone>()
            .AsNoTracking()
            .Where(milestone => contractIds.Contains(milestone.ContractsId))
            .ToListAsync(cancellationToken);

        var milestoneStatsByContractId = milestones
            .GroupBy(milestone => milestone.ContractsId)
            .ToDictionary(
                group => group.Key,
                group => new MilestoneStats(
                    group.Count(),
                    group.Count(milestone =>
                        string.IsNullOrWhiteSpace(milestone.Title) ||
                        milestone.Amount <= 0)));

        return sourceList.ToDictionary(
            source => source.JobPostId,
            source =>
            {
                contractByJobPostId.TryGetValue(source.JobPostId, out var contract);
                documentByJobPostId.TryGetValue(source.JobPostId, out var document);

                var milestoneStats = contract is null
                    ? null
                    : milestoneStatsByContractId.GetValueOrDefault(contract.ContractsId);
                var hasMilestones = milestoneStats is not null && milestoneStats.TotalCount > 0;
                var hasValidMilestones = hasMilestones && milestoneStats!.InvalidCount == 0;

                return BuildProgress(
                    source,
                    contract?.ContractsId,
                    document?.EsignDocumentsId,
                    document?.Status,
                    hasMilestones,
                    hasValidMilestones);
            });
    }

    private static JobPostSetupProgressDto BuildProgress(
        JobPostSetupSource source,
        Guid? contractId,
        Guid? esignDocumentId,
        int? esignStatus,
        bool hasMilestones,
        bool hasValidMilestones)
    {
        var isDetailsComplete = IsDetailsComplete(source);
        var isESignComplete = esignStatus == (int)ESignDocumentStatus.FullySigned;
        var canPublish = source.Status == DraftJobPostStatus &&
            isDetailsComplete &&
            contractId.HasValue &&
            isESignComplete &&
            hasValidMilestones;

        return new JobPostSetupProgressDto
        {
            IsDetailsComplete = isDetailsComplete,
            ContractId = contractId,
            ESignDocumentId = esignDocumentId,
            ESignStatus = esignStatus,
            HasMilestones = hasMilestones,
            CanPublish = canPublish,
            NextIncompleteStep = GetNextStep(
                source.Status,
                isDetailsComplete,
                isESignComplete,
                hasValidMilestones,
                canPublish)
        };
    }

    private static string GetNextStep(
        int jobPostStatus,
        bool isDetailsComplete,
        bool isESignComplete,
        bool hasValidMilestones,
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

        if (!isESignComplete)
        {
            return JobPostSetupStepNames.ESign;
        }

        if (!hasValidMilestones)
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
              !string.IsNullOrWhiteSpace(source.Description)
            : true;
    }

    private sealed record JobPostSetupSource(
        Guid JobPostId,
        string Title,
        string Description,
        int Status);

    private sealed record MilestoneStats(int TotalCount, int InvalidCount);
}
