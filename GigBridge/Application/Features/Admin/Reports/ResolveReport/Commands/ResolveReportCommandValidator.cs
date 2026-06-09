using FluentValidation;

namespace Application.Features.Admin.Reports.ResolveReport.Commands;

public class ResolveReportCommandValidator : AbstractValidator<ResolveReportCommand>
{
    public ResolveReportCommandValidator()
    {
        RuleFor(command => command.ReportId)
            .NotEmpty();

        RuleFor(command => command.AdminId)
            .NotEmpty();

        RuleFor(command => command.Request.AdminNote)
            .MaximumLength(2000);
    }
}
