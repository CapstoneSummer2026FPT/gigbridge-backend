using Application.Features.JobPosts.CreateJobPost.DTOs;
using MediatR;
using System;

namespace Application.Features.JobPosts.CreateJobPost.Commands;

public record CreateJobPostCommand(CreateJobPostRequest Request, Guid ClientProfilesId) : IRequest<Guid>;