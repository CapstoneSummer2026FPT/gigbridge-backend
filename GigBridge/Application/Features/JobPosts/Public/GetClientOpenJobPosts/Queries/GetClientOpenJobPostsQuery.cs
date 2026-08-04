using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Public.GetClientOpenJobPosts.Queries;

public record GetClientOpenJobPostsQuery(
    Guid ClientUserId,
    int PageIndex = 1,
    int PageSize = 50
) : IRequest<IEnumerable<JobPostSummaryDto>>;
