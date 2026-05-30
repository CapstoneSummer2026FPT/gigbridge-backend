using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Application.Features.JobPosts.GetAvailableJobPosts.Queries;

public record GetAvailableJobPostsQuery(int PageIndex = 1, int PageSize = 10) : IRequest<IEnumerable<JobPostSummaryDto>>;