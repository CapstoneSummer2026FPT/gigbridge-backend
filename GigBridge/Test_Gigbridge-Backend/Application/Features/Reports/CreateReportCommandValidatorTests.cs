using Application.Features.Reports.Public.CreateReport.Commands;
using Application.Features.Reports.Public.CreateReport.DTOs;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Application.Features.Reports;

public class CreateReportCommandValidatorTests
{
    private readonly CreateReportCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsErrorWhenEntityTypeIsUnsupported()
    {
        var command = new CreateReportCommand(
            new CreateReportRequest(Guid.NewGuid(), "Contract", ReportType.Spam, "Suspicious content."),
            Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.ReportedEntityType");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenReportTypeIsUnsupported()
    {
        var command = new CreateReportCommand(
            new CreateReportRequest(Guid.NewGuid(), ReportedEntityTypes.User, (ReportType)99, "Suspicious content."),
            Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Type");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenReasonIsEmpty()
    {
        var command = new CreateReportCommand(
            new CreateReportRequest(Guid.NewGuid(), ReportedEntityTypes.User, ReportType.Spam, ""),
            Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Reason");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenReasonIsTooLong()
    {
        var command = new CreateReportCommand(
            new CreateReportRequest(Guid.NewGuid(), ReportedEntityTypes.User, ReportType.Spam, new string('a', 2001)),
            Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Request.Reason");
    }
}
