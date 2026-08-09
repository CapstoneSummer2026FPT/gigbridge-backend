using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Freelancer.GetRecommendedJobPosts.DTOs;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Freelancer.GetRecommendedJobPosts.Queries;

public sealed class GetRecommendedJobPostsQueryHandler(
    IApplicationDbContext context,
    IAiServiceClient aiServiceClient)
    : IRequestHandler<GetRecommendedJobPostsQuery, List<RecommendedJobPostDto>>
{
    private const string AlgorithmVersion = "2.0";
    private const string ScoringVersion = "weighted-features-v1";

    public async Task<List<RecommendedJobPostDto>> Handle(
        GetRecommendedJobPostsQuery query,
        CancellationToken cancellationToken)
    {
        var pool = await RecommendedJobPoolLoader.LoadAsync(context, query.UserId, cancellationToken);
        if (pool.Candidates.Count == 0)
        {
            return [];
        }

        var aiTopK = Math.Min(30, pool.Candidates.Count);
        var aiResult = await RequestValidAiResultAsync(pool, aiTopK, cancellationToken);

        var candidatesById = pool.Candidates.ToDictionary(candidate => candidate.JobPostsId);
        var matchedJobIds = aiResult.Matches
            .Select(match => Guid.Parse(match.JobId))
            .ToHashSet();
        var aiInterviewJobIds = matchedJobIds.Count == 0
            ? []
            : (await context.Set<AiInterviewDefinition>()
                .AsNoTracking()
                .Where(definition =>
                    matchedJobIds.Contains(definition.JobPostId) &&
                    definition.Status != AiInterviewDefinitionStatus.Closed)
                .Select(definition => definition.JobPostId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();
        var now = DateTime.UtcNow;

        var evaluated = aiResult.Matches
            .Select(match =>
            {
                var jobId = Guid.Parse(match.JobId);
                var jobPost = candidatesById[jobId];
                var embedding = Round((decimal)match.EmbeddingScore);
                var algorithm = Round((decimal)match.AlgorithmScore);
                var finalScore = Round(Math.Clamp(0.5625m * embedding + 0.4375m * algorithm, 0m, 100m));
                var reasons = match.SemanticStrengths
                    .Concat(match.MatchReasons)
                    .Where(reason => !string.IsNullOrWhiteSpace(reason))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();

                var summary = JobPostProjection.ToSummaryDtos(
                    [jobPost], now, aiInterviewJobIds)[0];

                return new RecommendedJobPostDto(summary, finalScore, reasons);
            })
            .OrderByDescending(match => match.MatchPercentage)
            .ThenBy(match => match.JobPost.JobPostsId)
            .Take(query.TopK)
            .ToList();

        return evaluated;
    }

    private async Task<JobRerankResponseDto> RequestValidAiResultAsync(
        RecommendedJobPool pool,
        int topK,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await aiServiceClient.RerankJobsForFreelancerAsync(
                    BuildRequest(pool, topK), cancellationToken);
                ValidateAiResponse(response, pool, topK);
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ExternalServiceException or HttpRequestException or TaskCanceledException)
            {
                lastException = exception;
                if (attempt == 0)
                {
                    await Task.Delay(150, cancellationToken);
                }
            }
        }

        throw new ExternalServiceException(
            "AI job matching failed after retry.", lastException ?? new InvalidOperationException());
    }

    private static void ValidateAiResponse(
        JobRerankResponseDto response,
        RecommendedJobPool pool,
        int topK)
    {
        var expected = Math.Min(topK, pool.Candidates.Count);
        if (response.Matches.Count != expected)
        {
            throw new ExternalServiceException("AI job matching returned an incomplete candidate set.");
        }

        var knownIds = pool.Candidates.Select(candidate => candidate.JobPostsId).ToHashSet();
        var returnedIds = new HashSet<Guid>();
        foreach (var match in response.Matches)
        {
            if (!Guid.TryParse(match.JobId, out var jobId) ||
                !knownIds.Contains(jobId) ||
                !returnedIds.Add(jobId) ||
                match.EmbeddingScore is < 0d or > 100d ||
                match.AlgorithmScore is < 0d or > 100d)
            {
                throw new ExternalServiceException("AI job matching returned an invalid candidate result.");
            }
        }
    }

    private static JobRerankRequestDto BuildRequest(RecommendedJobPool pool, int topK) => new()
    {
        TopK = topK,
        AlgorithmVersion = AlgorithmVersion,
        ScoringVersion = ScoringVersion,
        Freelancer = new JobRerankFreelancerDto
        {
            FreelancerId = pool.Freelancer.FreelancerProfileId.ToString(),
            Title = Truncate(pool.Freelancer.Title, 300),
            Bio = Truncate(pool.Freelancer.Bio, 1200),
            Location = Truncate(pool.Freelancer.Location, 300),
            Availability = pool.Freelancer.Availability,
            MajorId = pool.Freelancer.MajorId?.ToString(),
            MajorName = pool.Freelancer.MajorName,
            Categories = pool.Freelancer.CategoryNames.ToList(),
            Skills = pool.Freelancer.Skills.Select(skill => skill.Name).ToList(),
            VerifiedWork = pool.Freelancer.VerifiedWork.Select(work => new JobRerankVerifiedWorkDto
            {
                ContractId = work.ContractId.ToString(),
                Title = Truncate(work.Title, 300) ?? string.Empty,
                Description = Truncate(work.Description, 500),
                MajorName = work.MajorName,
                CategoryName = work.CategoryName,
                Skills = work.Skills.Select(skill => skill.Name).ToList()
            }).ToList()
        },
        Candidates = pool.Candidates.Select(jobPost => new JobRerankCandidateDto
        {
            JobId = jobPost.JobPostsId.ToString(),
            Title = jobPost.Title,
            Description = Truncate(jobPost.Description, 4000) ?? string.Empty,
            Industry = jobPost.ClientProfiles?.Industry,
            MajorId = jobPost.MajorCategory?.MajorId.ToString(),
            MajorName = jobPost.MajorCategory?.Major?.Name,
            MajorCategoryId = jobPost.MajorCategoryId?.ToString(),
            CategoryName = jobPost.MajorCategory?.Category?.Name,
            Skills = jobPost.JobPostSkills
                .Where(selection => selection.Skills is not null)
                .Select(selection => selection.Skills.Name).ToList(),
            CustomSkills = jobPost.CustomSkillNames.ToList(),
            Location = jobPost.Location,
            EstimatedDuration = jobPost.EstimatedDuration
        }).ToList()
    };

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maximumLength ? value : value[..maximumLength];

    private static decimal Round(decimal value) => Math.Round(value, 0);
}
