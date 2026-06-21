using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;

public sealed record SaveDraftJobPostCommand(
    Guid JobPostId,
    Guid UserId,
    SaveDraftJobPostRequest Request) : IRequest<bool>;
