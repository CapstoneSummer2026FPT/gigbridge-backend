using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.GetMyJobPosts.Queries;

public class GetMyJobPostsQuery : IRequest<IEnumerable<JobPostSummaryDto>>
{
    public Guid UserId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}