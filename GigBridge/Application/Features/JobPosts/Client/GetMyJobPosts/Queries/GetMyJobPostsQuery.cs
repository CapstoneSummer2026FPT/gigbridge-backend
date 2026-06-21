using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.GetMyJobPosts.Queries;

public class GetMyJobPostsQuery : IRequest<IEnumerable<GetMyJobPostDto>>
{
    public Guid UserId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
