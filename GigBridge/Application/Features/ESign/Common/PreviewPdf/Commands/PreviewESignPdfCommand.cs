using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.PreviewPdf.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.PreviewPdf.Commands;

public sealed record PreviewESignPdfCommand(
    Guid DocumentId,
    Guid UserId,
    PreviewESignPdfRequest Request,
    string? IpAddress,
    string? UserAgent)
    : IRequest<ESignDocumentDownloadResponse>;
