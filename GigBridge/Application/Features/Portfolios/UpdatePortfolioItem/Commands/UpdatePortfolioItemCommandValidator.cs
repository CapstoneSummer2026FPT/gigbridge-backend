using Application.Features.Portfolios.Common;
using FluentValidation;

namespace Application.Features.Portfolios.UpdatePortfolioItem.Commands;

public sealed class UpdatePortfolioItemCommandValidator : AbstractValidator<UpdatePortfolioItemCommand>
{
    public UpdatePortfolioItemCommandValidator()
    {
        RuleFor(command => command.PortfolioItemId).NotEmpty();
        RuleFor(command => command.Dto)
            .NotNull().WithMessage("Portfolio data is required.")
            .SetValidator(new PortfolioItemInputDtoValidator());
        RuleFor(command => command)
            .Must(command => !(command.Image is not null && command.RemoveImage))
            .WithMessage("A portfolio image cannot be uploaded and removed in the same request.");
        RuleFor(command => command)
            .Must(command => !(command.PreserveExistingImage &&
                (command.Image is not null || command.RemoveImage)))
            .WithMessage("The existing portfolio image cannot be preserved and changed in the same request.");
    }
}
