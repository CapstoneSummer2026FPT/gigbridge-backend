using Application.Features.Contracts.Common.Internal;
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
                .MaximumLength(1_000_000)
                .When(command => !string.IsNullOrWhiteSpace(command.Request.SignatureImageUrl));
            RuleFor(command => command.Request.IdentityOrTaxCode)
                .Must(ContractIdentityCode.IsValid)
                .WithMessage("Identity code must contain exactly 9 or 12 digits.");
            RuleFor(command => command.Request.SignatureWidth)
                .InclusiveBetween(1, 1200)
                .When(command => command.Request.SignatureWidth.HasValue);
            RuleFor(command => command.Request.SignatureHeight)
                .InclusiveBetween(1, 500)
                .When(command => command.Request.SignatureHeight.HasValue);
        });
    }
}
