using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using MediatR;
using System;

namespace Application.Features.JobPosts.Public.GetJobPostDetail.Queries;

public record GetJobPostDetailQuery(Guid JobPostsId) : IRequest<JobPostDetailDto>;