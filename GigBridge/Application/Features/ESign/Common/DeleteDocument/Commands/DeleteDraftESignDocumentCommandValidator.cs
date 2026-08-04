using FluentValidation;

namespace Application.Features.ESign.Common.DeleteDocument.Commands;

public sealed class DeleteDraftESignDocumentCommandValidator
    : AbstractValidator<DeleteDraftESignDocumentCommand>
{
    public DeleteDraftESignDocumentCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.AdminUserId).NotEmpty();
    }
}
