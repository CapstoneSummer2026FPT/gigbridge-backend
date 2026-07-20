using MediatR;

namespace Application.Features.ESign.Common.DeleteDocument.Commands;

public sealed record DeleteDraftESignDocumentCommand(
    Guid DocumentId,
    Guid AdminUserId) : IRequest<bool>;
