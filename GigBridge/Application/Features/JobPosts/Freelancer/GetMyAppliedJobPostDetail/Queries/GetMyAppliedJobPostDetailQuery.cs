using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Freelancer.GetMyAppliedJobPostDetail.Queries;

public sealed record GetMyAppliedJobPostDetailQuery(
    Guid UserId,
    Guid JobPostId) : IRequest<JobPostDetailDto>;
