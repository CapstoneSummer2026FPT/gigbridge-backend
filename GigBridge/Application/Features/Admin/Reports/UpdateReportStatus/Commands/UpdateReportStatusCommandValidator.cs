using Domain.Enums;
using FluentValidation;

namespace Application.Features.Admin.Reports.UpdateReportStatus.Commands;

public class UpdateReportStatusCommandValidator : AbstractValidator<UpdateReportStatusCommand>
{
    public UpdateReportStatusCommandValidator()
    {
        RuleFor(command => command.ReportId)
            .NotEmpty();

        RuleFor(command => command.Request.Status)
            .Must(status => status is ReportStatus.Reviewing or ReportStatus.Dismissed)
            .WithMessage("Status can only be updated to Reviewing or Dismissed.");

        RuleFor(command => command.Request.AdminNote)
            .MaximumLength(2000);
    }
}
