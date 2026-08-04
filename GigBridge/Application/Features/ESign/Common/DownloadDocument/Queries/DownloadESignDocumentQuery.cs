using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.DownloadDocument.Queries;

public sealed record DownloadESignDocumentQuery(
    Guid DocumentId,
    Guid UserId) : IRequest<ESignDocumentDownloadResponse>;
