using FluentValidation;

namespace Application.Features.Reviews.Common.CreateReview.Commands;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(command => command.Request)
            .NotNull()
            .WithMessage("Request body is required.");

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.ContractId)
                .NotEmpty()
                .WithMessage("ContractId is required.");

            RuleFor(command => command.Request.Rating)
                .InclusiveBetween(1, 5)
                .WithMessage("Rating must be between 1 and 5.");

            RuleFor(command => command.Request.Comment)
                .MaximumLength(1000)
                .WithMessage("Comment must be 1000 characters or fewer.");

            RuleFor(command => command.Request.CommunicationRating)
                .InclusiveBetween(1, 5)
                .When(command => command.Request.CommunicationRating.HasValue)
                .WithMessage("CommunicationRating must be between 1 and 5.");

            RuleFor(command => command.Request.QualityRating)
                .InclusiveBetween(1, 5)
                .When(command => command.Request.QualityRating.HasValue)
                .WithMessage("QualityRating must be between 1 and 5.");

            RuleFor(command => command.Request.TimelinessRating)
                .InclusiveBetween(1, 5)
                .When(command => command.Request.TimelinessRating.HasValue)
                .WithMessage("TimelinessRating must be between 1 and 5.");
        });
    }
}
