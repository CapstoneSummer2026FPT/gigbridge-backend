using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using MediatR;
using System;

namespace Application.Features.JobPosts.Client.CreateJobPost.Commands;

public record CreateJobPostCommand(CreateJobPostRequest Request, Guid UserId) : IRequest<Guid>;