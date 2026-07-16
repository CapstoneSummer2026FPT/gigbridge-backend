using FluentValidation;

namespace Application.Features.Disputes.Client.Create.Commands;

public sealed class CreateDisputeCommandValidator : AbstractValidator<CreateDisputeCommand>
{
    public CreateDisputeCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request.ContractId).NotEmpty();
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(4000);
    }
}
