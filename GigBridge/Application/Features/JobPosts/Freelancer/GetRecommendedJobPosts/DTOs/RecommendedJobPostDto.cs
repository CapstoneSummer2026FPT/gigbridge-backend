using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;

namespace Application.Features.JobPosts.Freelancer.GetRecommendedJobPosts.DTOs;

public sealed record RecommendedJobPostDto(
    JobPostSummaryDto JobPost,
    decimal MatchPercentage,
    IReadOnlyList<string> MatchReasons);
