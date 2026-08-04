using Application.Common.Models;
using Application.Features.Admin.Reports.GetReportDetail.Queries;
using Application.Features.Admin.Reports.GetReports.Queries;
using Application.Features.Admin.Reports.GetReportSummary.Queries;
using Application.Features.Admin.Reports.ResolveReport.Commands;
using Application.Features.Admin.Reports.ResolveReport.DTOs;
using Application.Features.Admin.Reports.UpdateReportStatus.Commands;
using Application.Features.Admin.Reports.UpdateReportStatus.DTOs;
using Application.Features.Reports.Common.DTOs;
using Application.Features.Admin.Reports.AccountReports;
using Application.Features.Reports.Evidence;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[Route("api/reports/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminReportsController : BaseApiController
{
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccountReports([FromQuery] GetAccountReportsQuery query)
        => Ok(ApiResponse<PaginatedList<AccountReportListItemDto>>.Ok(await Mediator.Send(query), "Account reports retrieved successfully."));

    [HttpGet("accounts/{reportId:guid}")]
    public async Task<IActionResult> GetAccountReport(Guid reportId)
        => Ok(ApiResponse<AccountReportDetailDto>.Ok(await Mediator.Send(new GetAccountReportDetailQuery(reportId)), "Account report retrieved successfully."));

    [HttpPut("accounts/{reportId:guid}/status")]
    public async Task<IActionResult> UpdateAccountReportStatus(Guid reportId, [FromBody] AccountReportStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse();
        return Ok(ApiResponse<AccountReportDetailDto>.Ok(await Mediator.Send(new UpdateAccountReportStatusCommand(adminId, reportId, request)), "Account report status updated successfully."));
    }

    [HttpPut("accounts/{reportId:guid}/resolve")]
    public async Task<IActionResult> ResolveAccountReport(Guid reportId, [FromBody] ResolveAccountReportRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse();
        return Ok(ApiResponse<AccountReportDetailDto>.Ok(await Mediator.Send(new ResolveAccountReportCommand(adminId, reportId, request)), "Account report resolved successfully."));
    }

    [HttpGet("accounts/{reportId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadAccountReportEvidence(Guid reportId, Guid evidenceId)
    {
        if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse();
        return Ok(ApiResponse<ReportEvidenceDownloadDto>.Ok(await Mediator.Send(new GetReportEvidenceDownloadQuery(reportId, evidenceId, adminId, true)), "Evidence download authorized."));
    }
    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] ReportType? type = null,
        [FromQuery] string? reportedEntityType = null,
        [FromQuery] Guid? reportedEntityId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isPremium = null)
    {
        var result = await Mediator.Send(new GetReportsQuery(page, pageSize, status, type, reportedEntityType, reportedEntityId, search, isPremium));
        return Ok(ApiResponse<ReportsResponse>.Ok(result, "Reports retrieved successfully."));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetReportSummary()
    {
        var result = await Mediator.Send(new GetReportSummaryQuery());
        return Ok(ApiResponse<ReportSummaryDto>.Ok(result, "Report summary retrieved successfully."));
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
        await Mediator.Send(new UpdateReportStatusCommand(reportId, adminId, request));
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
