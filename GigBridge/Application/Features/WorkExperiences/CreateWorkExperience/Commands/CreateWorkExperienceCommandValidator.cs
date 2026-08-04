using Application.Features.WorkExperiences.Common;
using FluentValidation;

namespace Application.Features.WorkExperiences.CreateWorkExperience.Commands;

public sealed class CreateWorkExperienceCommandValidator : AbstractValidator<CreateWorkExperienceCommand>
{
    public CreateWorkExperienceCommandValidator()
    {
        RuleFor(command => command.Dto)
            .NotNull().WithMessage("Work experience data is required.")
            .SetValidator(new WorkExperienceInputDtoValidator());
    }
}
