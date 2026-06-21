using Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.GetMyJobPostDetail.Queries;

public record GetMyJobPostDetailQuery(Guid UserId, Guid JobPostId) : IRequest<GetMyJobPostDetailDto>;
