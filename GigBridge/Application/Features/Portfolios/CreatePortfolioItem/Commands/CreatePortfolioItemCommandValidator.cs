using Application.Features.Portfolios.Common;
using FluentValidation;

namespace Application.Features.Portfolios.CreatePortfolioItem.Commands;

public sealed class CreatePortfolioItemCommandValidator : AbstractValidator<CreatePortfolioItemCommand>
{
    public CreatePortfolioItemCommandValidator()
    {
        RuleFor(command => command.Dto)
            .NotNull().WithMessage("Portfolio data is required.")
            .SetValidator(new PortfolioItemInputDtoValidator());
    }
}
