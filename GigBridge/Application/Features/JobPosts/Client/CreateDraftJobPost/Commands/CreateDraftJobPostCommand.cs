using Application.Features.JobPosts.Client.CreateDraftJobPost.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.CreateDraftJobPost.Commands;

public record CreateDraftJobPostCommand(Guid UserId) : IRequest<CreateDraftJobPostResponse>;
