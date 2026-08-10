using Application.Features.JobPosts.Freelancer.GetRecommendedJobPosts.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Freelancer.GetRecommendedJobPosts.Queries;

public sealed record GetRecommendedJobPostsQuery(
    Guid UserId,
    int TopK = 20
) : IRequest<List<RecommendedJobPostDto>>;
