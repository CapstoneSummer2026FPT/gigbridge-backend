using Domain.Enums;
using FluentValidation;

namespace Application.Features.Reports.Public.CreateReport.Commands;

public class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(command => command.ReporterId)
            .NotEmpty();

        RuleFor(command => command.Request.ReportedEntityId)
            .NotEmpty();

        RuleFor(command => command.Request.ReportedEntityType)
            .NotEmpty()
            .Must(ReportedEntityTypes.IsSupported)
            .WithMessage("ReportedEntityType must be User, JobPost, or Review.");

        RuleFor(command => command.Request.Type)
            .IsInEnum();

        RuleFor(command => command.Request.Reason)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
