using Application.Common.Models;
using Application.Features.Admin.Reports.GetReportDetail.Queries;
using Application.Features.Admin.Reports.GetReports.Queries;
using Application.Features.Admin.Reports.ResolveReport.Commands;
using Application.Features.Admin.Reports.ResolveReport.DTOs;
using Application.Features.Admin.Reports.UpdateReportStatus.Commands;
using Application.Features.Admin.Reports.UpdateReportStatus.DTOs;
using Application.Features.Reports.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[Route("api/reports/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminReportsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] ReportType? type = null,
        [FromQuery] string? reportedEntityType = null,
        [FromQuery] string? search = null)
    {
        var result = await Mediator.Send(new GetReportsQuery(page, pageSize, status, type, reportedEntityType, search));
        return Ok(ApiResponse<ReportsResponse>.Ok(result, "Reports retrieved successfully."));
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> GetReportDetail(Guid reportId)
    {
        var result = await Mediator.Send(new GetAdminReportDetailQuery(reportId));
        return Ok(ApiResponse<ReportDto>.Ok(result, "Report retrieved successfully."));
    }

    [HttpPut("{reportId:guid}/status")]
    public async Task<IActionResult> UpdateReportStatus(Guid reportId, [FromBody] UpdateReportStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
        {
            return InvalidTokenResponse();
        }
        await Mediator.Send(new UpdateReportStatusCommand(reportId, request));
        return Ok(ApiResponse<object>.Ok(null!, "Report status updated successfully."));
    }

    [HttpPut("{reportId:guid}/resolve")]
    public async Task<IActionResult> ResolveReport(Guid reportId, [FromBody] ResolveReportRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
        {
            return InvalidTokenResponse();
        }

        await Mediator.Send(new ResolveReportCommand(reportId, adminId, request));
        return Ok(ApiResponse<object>.Ok(null!, "Report resolved successfully."));
    }
}
