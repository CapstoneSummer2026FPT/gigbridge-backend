using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Client.GetDocumentByJobPost.Queries;

public sealed record GetESignDocumentByJobPostQuery(
    Guid JobPostId,
    Guid UserId) : IRequest<ESignDocumentResponse>;
