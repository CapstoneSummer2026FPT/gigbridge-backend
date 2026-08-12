using Domain.Enums.Reports;

namespace Application.Features.Admin.Reports.UpdateReportStatus.DTOs;

public record UpdateReportStatusRequest(ReportStatus Status, string? AdminNote);
