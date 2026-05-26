using Application.DTOs.Admin;
using Application.Features.Admin.Reports.Dto;
using MediatR;

namespace Application.Features.Admin.Reports.Command;

public sealed record StartReportReviewCommand(
    Guid ReportId,
    ReportReviewRequestDto Request,
    AdminActorDto Actor) : IRequest<ReportDto>;
