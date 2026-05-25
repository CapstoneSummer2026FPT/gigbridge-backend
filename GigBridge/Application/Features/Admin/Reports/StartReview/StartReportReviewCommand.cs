using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Reports.StartReview;

public sealed record StartReportReviewCommand(
    Guid ReportId,
    ReportReviewRequestDto Request,
    AdminActorDto Actor) : IRequest<ReportDto>;
