using FluentValidation;

namespace Application.Features.Admin.Users.Premium.Revoke.Commands;

public sealed class RevokeUserPremiumCommandValidator : AbstractValidator<RevokeUserPremiumCommand>
{
    public RevokeUserPremiumCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
