using Application.Common.Models;
using Application.Features.Reports.Common.DTOs;
using Application.Features.Reports.Public.CreateReport.Commands;
using Application.Features.Reports.Public.CreateReport.DTOs;
using Application.Features.Reports.Public.GetMyReports.Queries;
using Application.Features.Reports.Public.GetReportDetail.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[Authorize]
public class ReportsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var reportId = await Mediator.Send(new CreateReportCommand(request, userId));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.CreatedAt(reportId, "Report created successfully."));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyReports([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyReportsQuery(userId, page, pageSize));
        return Ok(ApiResponse<ReportsResponse>.Ok(result, "Reports retrieved successfully."));
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> GetReportDetail(Guid reportId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetReportDetailQuery(reportId, userId));
        return Ok(ApiResponse<ReportDto>.Ok(result, "Report retrieved successfully."));
    }
}
