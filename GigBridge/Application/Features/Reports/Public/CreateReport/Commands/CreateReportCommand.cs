using Application.Features.Reports.Public.CreateReport.DTOs;
using MediatR;

namespace Application.Features.Reports.Public.CreateReport.Commands;

public record CreateReportCommand(CreateReportRequest Request, Guid ReporterId) : IRequest<Guid>;
