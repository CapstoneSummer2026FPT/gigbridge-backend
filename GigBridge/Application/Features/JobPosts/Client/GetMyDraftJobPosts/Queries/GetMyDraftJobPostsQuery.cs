using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.GetMyDraftJobPosts.Queries;

public sealed record GetMyDraftJobPostsQuery(Guid UserId)
    : IRequest<IEnumerable<GetMyJobPostDto>>;
