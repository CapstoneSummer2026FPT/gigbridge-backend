using Domain.Enums;

namespace Application.Features.Reports.Public.CreateReport.DTOs;

public record CreateReportRequest(
    Guid ReportedEntityId,
    string ReportedEntityType,
    ReportType Type,
    string Reason);
