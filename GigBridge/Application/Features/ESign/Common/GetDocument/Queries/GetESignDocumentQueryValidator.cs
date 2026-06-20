using FluentValidation;

namespace Application.Features.ESign.Common.GetDocument.Queries;

public sealed class GetESignDocumentQueryValidator
    : AbstractValidator<GetESignDocumentQuery>
{
    public GetESignDocumentQueryValidator()
    {
        RuleFor(query => query.DocumentId)
            .NotEmpty()
            .WithMessage("DocumentId is required.");

        RuleFor(query => query.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
