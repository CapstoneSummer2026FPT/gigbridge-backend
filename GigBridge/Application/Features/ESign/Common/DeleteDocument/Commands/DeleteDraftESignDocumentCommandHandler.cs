using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.ESign.Services;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.DeleteDocument.Commands;

public sealed class DeleteDraftESignDocumentCommandHandler
    : IRequestHandler<DeleteDraftESignDocumentCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteDraftESignDocumentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        DeleteDraftESignDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var isAdmin = await _context.Set<User>()
            .AsNoTracking()
            .AnyAsync(
                user => user.UserId == request.AdminUserId && user.Role == (int)UserRole.Admin,
                cancellationToken);

        if (!isAdmin)
        {
            throw new ForbiddenAccessException("Only admins can delete draft e-sign documents.");
        }

        var document = await _context.Set<EsignDocument>()
            .FirstOrDefaultAsync(
                item => item.EsignDocumentsId == request.DocumentId,
                cancellationToken);

        if (document is null)
        {
            throw new NotFoundException("E-sign document does not exist.");
        }

        var hasSignatures = await _context.Set<EsignSignature>()
            .AsNoTracking()
            .AnyAsync(
                signature => signature.EsignDocumentsId == request.DocumentId,
                cancellationToken);

        if (document.Status != (int)ESignDocumentStatus.Draft || hasSignatures)
        {
            throw new ConflictException("Only unsigned draft e-sign documents can be deleted.");
        }

        var now = DateTime.UtcNow;
        ESignDocumentRevision.Advance(document, now);
        await ESignDocumentRevision.EnqueueAsync(
            _context,
            document,
            now,
            cancellationToken,
            ESignDocumentRevision.DeletedChangeKind);
        _context.Set<EsignDocument>().Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
