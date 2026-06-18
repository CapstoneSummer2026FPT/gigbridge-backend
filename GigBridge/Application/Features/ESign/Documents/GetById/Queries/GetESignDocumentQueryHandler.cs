using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using MediatR;

namespace Application.Features.ESign.Documents.GetById.Queries;

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
        var document = await ESignAccessGuard.GetDocumentAsync(
            _context,
            request.DocumentId,
            cancellationToken);

        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            _context,
            document,
            request.UserId,
            cancellationToken);

        return await ESignDocumentProjection.ToResponseAsync(_context, document, cancellationToken);
    }
}
