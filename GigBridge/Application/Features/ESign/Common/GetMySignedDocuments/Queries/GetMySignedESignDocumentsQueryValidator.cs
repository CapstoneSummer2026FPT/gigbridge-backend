using Domain.Enums;
using FluentValidation;

namespace Application.Features.ESign.Common.GetMySignedDocuments.Queries;

public sealed class GetMySignedESignDocumentsQueryValidator
    : AbstractValidator<GetMySignedESignDocumentsQuery>
{
    private static readonly string[] SupportedDocumentTypes = ["job", "contract"];

    public GetMySignedESignDocumentsQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(query => query.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(typeof(ESignDocumentStatus), status.Value))
            .WithMessage("Status is invalid.");

        RuleFor(query => query.DocumentType)
            .Must(documentType =>
                string.IsNullOrWhiteSpace(documentType) ||
                SupportedDocumentTypes.Contains(documentType.Trim().ToLowerInvariant()))
            .WithMessage("DocumentType must be 'job' or 'contract'.");
    }
}
