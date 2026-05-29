using Application.Features.JobPosts.CreateJobPost.Commands;
using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.GetAvailableJobPosts.Queries;
using Application.Features.JobPosts.GetJobPostDetail.DTOs;
using Application.Features.JobPosts.GetJobPostDetail.Queries;
using Application.Features.JobPosts.GetMyAppliedJobPosts.Queries;
using Application.Features.JobPosts.GetMyJobPosts.Queries;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.JobPosts.Services;

public interface IJobPostsService
{
    Task<Guid> CreateJobPostAsync(CreateJobPostCommand command, CancellationToken cancellationToken = default);

    Task<IEnumerable<JobPostSummaryDto>> GetAvailableJobPostsAsync(GetAvailableJobPostsQuery request, CancellationToken cancellationToken = default);

    Task<JobPostDetailDto> GetJobPostDetailAsync(GetJobPostDetailQuery request, CancellationToken cancellationToken = default);

    Task<IEnumerable<JobPostSummaryDto>> GetMyJobPostsAsync(GetMyJobPostsQuery request, CancellationToken cancellationToken = default);

    Task<IEnumerable<JobPostSummaryDto>> GetMyAppliedJobPostsAsync(GetMyAppliedJobPostsQuery request, CancellationToken cancellationToken = default);
}