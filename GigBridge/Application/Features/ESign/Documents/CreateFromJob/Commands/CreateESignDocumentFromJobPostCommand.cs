using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Documents.CreateFromJob.Commands;

public sealed record CreateESignDocumentFromJobPostCommand(
    Guid JobPostId,
    Guid UserId) : IRequest<ESignDocumentResponse>;
