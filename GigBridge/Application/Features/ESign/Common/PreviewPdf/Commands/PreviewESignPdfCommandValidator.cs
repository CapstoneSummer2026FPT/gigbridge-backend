using FluentValidation;

namespace Application.Features.ESign.Common.PreviewPdf.Commands;

public sealed class PreviewESignPdfCommandValidator : AbstractValidator<PreviewESignPdfCommand>
{
    public PreviewESignPdfCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.SignatureImageUrl)
                .NotEmpty()
                .MaximumLength(1_000_000);
            RuleFor(command => command.Request.SignatureWidth)
                .InclusiveBetween(100, 1200)
                .When(command => command.Request.SignatureWidth.HasValue);
            RuleFor(command => command.Request.SignatureHeight)
                .InclusiveBetween(40, 500)
                .When(command => command.Request.SignatureHeight.HasValue);
        });
    }
}
