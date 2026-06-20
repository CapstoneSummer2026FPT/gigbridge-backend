using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;

public sealed record CreateESignDocumentFromJobPostCommand(
    Guid JobPostId,
    Guid UserId) : IRequest<ESignDocumentResponse>;
