using Domain.Enums.ESign;
using FluentValidation;

namespace Application.Features.ESign.Common.GetDocuments.Queries;

public sealed class GetESignDocumentsQueryValidator : AbstractValidator<GetESignDocumentsQuery>
{
    public GetESignDocumentsQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(typeof(ESignDocumentStatus), status.Value))
            .WithMessage("Status is invalid.");
        RuleFor(query => query.Q).MaximumLength(200);
    }
}
