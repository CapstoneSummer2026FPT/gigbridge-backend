using FluentValidation;

namespace Application.Features.ESign.Common.DownloadDocument.Queries;

public sealed class DownloadESignDocumentQueryValidator : AbstractValidator<DownloadESignDocumentQuery>
{
    public DownloadESignDocumentQueryValidator()
    {
        RuleFor(query => query.DocumentId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}
