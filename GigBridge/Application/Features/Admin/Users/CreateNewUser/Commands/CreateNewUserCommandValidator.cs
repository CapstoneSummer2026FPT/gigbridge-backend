using FluentValidation;

namespace Application.Features.Admin.Users.CreateNewUser.Commands;

public class CreateNewUserCommandValidator : AbstractValidator<CreateNewUserCommand>
{
    public CreateNewUserCommandValidator()
    {
        RuleFor(x => x.Request.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters");

        RuleFor(x => x.Request.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");

        RuleFor(x => x.Request.Role)
            .InclusiveBetween(0, 2).WithMessage("Role must be 0 (Client), 1 (Freelancer), or 2 (Admin)");

        When(x => x.Request.PhoneNumber is not null, () =>
        {
            RuleFor(x => x.Request.PhoneNumber)
                .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters");
        });
    }
}
