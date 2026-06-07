using FluentValidation;

namespace Application.Features.Admin.Users.UpdateUser.Commands;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        When(x => x.Request.FullName is not null, () =>
        {
            RuleFor(x => x.Request.FullName)
                .MaximumLength(100).WithMessage("Full name must not exceed 100 characters");
        });

        When(x => x.Request.PhoneNumber is not null, () =>
        {
            RuleFor(x => x.Request.PhoneNumber)
                .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters");
        });

        When(x => x.Request.PreferredLanguage is not null, () =>
        {
            RuleFor(x => x.Request.PreferredLanguage)
                .MaximumLength(10).WithMessage("Preferred language must not exceed 10 characters");
        });
    }
}
