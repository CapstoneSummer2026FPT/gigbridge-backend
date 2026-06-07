using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.Register.Commands
{
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(v => v.RegisterRequest.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Email must be a valid email address.");

            RuleFor(v => v.RegisterRequest.FullName)
                .MaximumLength(100)
                .WithMessage("Full name cannot exceed 100 characters.");

            RuleFor(v => v.RegisterRequest.Password)
                .NotEmpty().WithMessage("Password is required.")
                .Matches(@"^(?=\S{8,}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).*$")
                .WithMessage("Password must contain at least 8 characters, one uppercase letter, one lowercase letter, one number, and one special character.");

            RuleFor(v => v.RegisterRequest.ConfirmPassword)
                .NotEmpty().WithMessage("Confirm password is required.")
                .Equal(v => v.RegisterRequest.Password)
                .WithMessage("Passwords do not match.");

            RuleFor(v => v.RegisterRequest.role)
               .NotNull().WithMessage("Role is required.")
               .IsInEnum().WithMessage("Invalid role.");
        }
    }
}
