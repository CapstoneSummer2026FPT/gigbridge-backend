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
                .NotNull()
                .WithMessage("CommunicationRating is required.")
                .InclusiveBetween(1, 5)
                .WithMessage("CommunicationRating must be between 1 and 5.");

            RuleFor(command => command.Request.QualityRating)
                .NotNull()
                .WithMessage("QualityRating is required.")
                .InclusiveBetween(1, 5)
                .WithMessage("QualityRating must be between 1 and 5.");

            RuleFor(command => command.Request.TimelinessRating)
                .NotNull()
                .WithMessage("TimelinessRating is required.")
                .InclusiveBetween(1, 5)
                .WithMessage("TimelinessRating must be between 1 and 5.");

            RuleFor(command => command.Request.IsAnonymous)
                .Equal(false)
                .WithMessage("Anonymous reviews are no longer supported.");
        });
    }
}
