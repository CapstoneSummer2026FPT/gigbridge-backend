using Application.DTOs.Admin;
using Application.Common.Models;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/reports")]
public sealed class AdminReportsController : AdminControllerBase
{
    private readonly IAdminReportService _reportService;

    public AdminReportsController(IAdminReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ReportPageQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _reportService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Reports retrieved successfully"));
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> Get(Guid reportId, CancellationToken cancellationToken)
    {
        var data = await _reportService.GetAsync(reportId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Report retrieved successfully"));
    }

    [HttpPatch("{reportId:guid}/review")]
    public async Task<IActionResult> Review(Guid reportId, [FromBody] ReportReviewRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _reportService.ReviewAsync(reportId, request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Report moved to review successfully"));
    }

    [HttpPatch("{reportId:guid}/resolution")]
    public async Task<IActionResult> Resolve(Guid reportId, [FromBody] ReportResolutionRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _reportService.ResolveAsync(reportId, request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Report resolution recorded successfully"));
    }
}
