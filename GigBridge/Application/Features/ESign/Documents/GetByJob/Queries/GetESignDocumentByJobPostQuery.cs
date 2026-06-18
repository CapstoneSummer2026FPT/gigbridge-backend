using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Documents.GetByJob.Queries;

public sealed record GetESignDocumentByJobPostQuery(
    Guid JobPostId,
    Guid UserId) : IRequest<ESignDocumentResponse>;
