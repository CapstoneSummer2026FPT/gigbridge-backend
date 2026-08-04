using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Enums;
using MediatR;

namespace Application.Features.ESign.Common.DownloadDocument.Queries;

public sealed class DownloadESignDocumentQueryHandler
    : IRequestHandler<DownloadESignDocumentQuery, ESignDocumentDownloadResponse>
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private readonly IApplicationDbContext _context;

    public DownloadESignDocumentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ESignDocumentDownloadResponse> Handle(
        DownloadESignDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await ESignAccessGuard.GetDocumentAsync(
            _context,
            request.DocumentId,
            cancellationToken);

        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            _context,
            document,
            request.UserId,
            cancellationToken);

        if (document.Status != (int)ESignDocumentStatus.FullySigned &&
            document.Status != (int)ESignDocumentStatus.Voided)
        {
            throw new ConflictException("The finalized e-sign document is not ready for download.");
        }

        if (document.FinalizedDocumentContent is not { Length: > 0 })
        {
            throw new ConflictException("The finalized e-sign document artifact is unavailable.");
        }

        var fileName = Path.GetFileName(document.FinalizedDocumentFileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{document.DocumentCode}.docx";
        }

        return new ESignDocumentDownloadResponse(
            document.FinalizedDocumentContent,
            fileName,
            string.IsNullOrWhiteSpace(document.FinalizedDocumentMimeType)
                ? DocxContentType
                : document.FinalizedDocumentMimeType);
    }
}
