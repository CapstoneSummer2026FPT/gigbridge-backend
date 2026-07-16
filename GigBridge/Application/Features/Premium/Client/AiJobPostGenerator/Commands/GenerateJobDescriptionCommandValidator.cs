using FluentValidation;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public sealed class GenerateJobDescriptionCommandValidator : AbstractValidator<GenerateJobDescriptionCommand>
{
    public GenerateJobDescriptionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ClientPrompt).NotEmpty().MaximumLength(2000);
    }
}
