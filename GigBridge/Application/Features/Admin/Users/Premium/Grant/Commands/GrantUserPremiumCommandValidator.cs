using FluentValidation;

namespace Application.Features.Admin.Users.Premium.Grant.Commands;

public sealed class GrantUserPremiumCommandValidator : AbstractValidator<GrantUserPremiumCommand>
{
    public GrantUserPremiumCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
