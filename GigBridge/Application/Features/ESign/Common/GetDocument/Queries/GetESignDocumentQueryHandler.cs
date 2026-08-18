using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.GetDocument.Queries;

public sealed class GetESignDocumentQueryHandler
    : IRequestHandler<GetESignDocumentQuery, ESignDocumentResponse>
{
    private readonly IApplicationDbContext _context;

    public GetESignDocumentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ESignDocumentResponse> Handle(
        GetESignDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var readModel = await _context.Set<EsignDocument>()
            .Where(document => document.EsignDocumentsId == request.DocumentId)
            .SelectForResponse()
            .FirstOrDefaultAsync(cancellationToken);

        if (readModel is null)
        {
            throw new NotFoundException("E-sign document does not exist.");
        }

        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            _context,
            readModel.Document,
            request.UserId,
            cancellationToken);

        return await ESignDocumentProjection.ToResponseAsync(
            _context,
            readModel.Document,
            request.UserId,
            cancellationToken,
            readModel.HasFinalizedDocumentContent,
            readModel.HasPdfDocumentContent);
    }
}
