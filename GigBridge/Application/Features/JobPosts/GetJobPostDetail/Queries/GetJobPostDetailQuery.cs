using Application.Features.JobPosts.GetJobPostDetail.DTOs;
using MediatR;
using System;

namespace Application.Features.JobPosts.GetJobPostDetail.Queries;

public record GetJobPostDetailQuery(Guid JobPostsId) : IRequest<JobPostDetailDto>;