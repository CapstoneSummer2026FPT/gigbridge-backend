using Application.Common.Models;
using Application.Features.Reports.Common.DTOs;
using Application.Features.Reports.Public.CreateReport.Commands;
using Application.Features.Reports.Public.CreateReport.DTOs;
using Application.Features.Reports.Public.GetMyReports.Queries;
using Application.Features.Reports.Public.GetReportDetail.Queries;
using Application.Features.Reports.Evidence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Reports.Common;

[Authorize]
public class ReportsController : BaseApiController
{
    [HttpPost("{reportId:guid}/evidence")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> AddEvidence(Guid reportId, [FromForm] List<IFormFile> files, [FromForm] string? description = null)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var inputs = files.Select(x => new ReportEvidenceFile(x.OpenReadStream(), x.FileName, x.ContentType, x.Length, description)).ToList();
        try
        {
            var result = await Mediator.Send(new AddReportEvidenceCommand(reportId, userId, inputs));
            return StatusCode(StatusCodes.Status201Created, ApiResponse<IReadOnlyList<Application.Features.Admin.Reports.AccountReports.AccountReportEvidenceDto>>.CreatedAt(result, "Evidence uploaded successfully."));
        }
        finally
        {
            foreach (var input in inputs) input.Content.Dispose();
        }
    }

    [HttpGet("{reportId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadEvidence(Guid reportId, Guid evidenceId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        return Ok(ApiResponse<ReportEvidenceDownloadDto>.Ok(await Mediator.Send(new GetReportEvidenceDownloadQuery(reportId, evidenceId, userId, false)), "Evidence download authorized."));
    }
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
