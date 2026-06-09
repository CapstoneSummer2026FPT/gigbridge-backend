using Application.Features.Admin.Reports.UpdateReportStatus.DTOs;
using MediatR;

namespace Application.Features.Admin.Reports.UpdateReportStatus.Commands;

public record UpdateReportStatusCommand(Guid ReportId, UpdateReportStatusRequest Request) : IRequest;
