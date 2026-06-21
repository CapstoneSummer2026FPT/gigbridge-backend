using FluentValidation;

namespace Application.Features.ESign.Client.SubmitSignature.Commands;

public sealed class SubmitESignSignatureCommandValidator
    : AbstractValidator<SubmitESignSignatureCommand>
{
    public SubmitESignSignatureCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(command => command.Request)
            .NotNull()
            .WithMessage("Request body is required.");

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.DocumentId)
                .NotEmpty()
                .WithMessage("DocumentId is required.");

            RuleFor(command => command.Request.SignatureImageUrl)
                .NotEmpty()
                .WithMessage("Signature image is required.")
                .Must(value => !string.IsNullOrWhiteSpace(value) &&
                    value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Signature image must be an image data URI.")
                .MaximumLength(1_000_000)
                .WithMessage("Signature image is too large.");

            RuleFor(command => command.Request.SignatureWidth)
                .GreaterThan(0)
                .When(command => command.Request.SignatureWidth.HasValue)
                .WithMessage("SignatureWidth must be greater than 0.");

            RuleFor(command => command.Request.SignatureHeight)
                .GreaterThan(0)
                .When(command => command.Request.SignatureHeight.HasValue)
                .WithMessage("SignatureHeight must be greater than 0.");
        });
    }
}
