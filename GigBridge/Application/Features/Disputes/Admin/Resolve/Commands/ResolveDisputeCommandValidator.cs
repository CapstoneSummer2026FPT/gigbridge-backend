using FluentValidation;

namespace Application.Features.Disputes.Admin.Resolve.Commands;

public sealed class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.DisputeId).NotEmpty();
        RuleFor(x => x.Request.Resolution).InclusiveBetween(0, 3);
        RuleFor(x => x.Request.ResolutionNote).NotEmpty().MaximumLength(4000);
    }
}
