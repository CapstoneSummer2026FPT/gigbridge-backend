using FluentValidation;

namespace Application.Features.ESign.Documents.GetByJob.Queries;

public sealed class GetESignDocumentByJobPostQueryValidator
    : AbstractValidator<GetESignDocumentByJobPostQuery>
{
    public GetESignDocumentByJobPostQueryValidator()
    {
        RuleFor(query => query.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");

        RuleFor(query => query.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
