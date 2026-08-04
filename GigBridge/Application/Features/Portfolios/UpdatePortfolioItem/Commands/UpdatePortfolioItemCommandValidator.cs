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
    }
}
