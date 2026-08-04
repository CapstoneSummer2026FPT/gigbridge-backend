using Application.Features.WorkExperiences.Common;
using FluentValidation;

namespace Application.Features.WorkExperiences.UpdateWorkExperience.Commands;

public sealed class UpdateWorkExperienceCommandValidator : AbstractValidator<UpdateWorkExperienceCommand>
{
    public UpdateWorkExperienceCommandValidator()
    {
        RuleFor(command => command.WorkExperienceId).NotEmpty();
        RuleFor(command => command.Dto)
            .NotNull().WithMessage("Work experience data is required.")
            .SetValidator(new WorkExperienceInputDtoValidator());
    }
}
