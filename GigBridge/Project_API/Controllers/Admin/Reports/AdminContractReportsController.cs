using Application.Common.Models;
using Application.Features.Admin.ContractReports;
using Application.Features.ReportContracts.Escalate.Commands;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin.Reports;

[ApiController, Route("api/admin/contract-reports"), Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminContractReportsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetAdminContractReportsQuery query) =>
        Ok(ApiResponse<PaginatedList<AdminContractReportListItem>>.Ok(await Mediator.Send(query), "Contract Reports retrieved successfully."));

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> Detail(Guid reportId)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new GetAdminContractReportDetailQuery(reportId, adminId, true)), "Contract Report retrieved successfully.")); }

    [HttpPost("{reportId:guid}/assign")]
    public async Task<IActionResult> Assign(Guid reportId, [FromBody] AssignContractReportRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new AssignContractReportCommand(adminId, reportId, request.AdminId)), "Contract Report assigned.")); }

    [HttpPost("{reportId:guid}/request-information")]
    public async Task<IActionResult> RequestInformation(Guid reportId, [FromBody] RequestContractReportInformationRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new RequestContractReportInformationCommand(adminId, reportId, request)), "Additional information requested.")); }

    [HttpPost("{reportId:guid}/close")]
    public async Task<IActionResult> Close(Guid reportId, [FromBody] CloseContractReportRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new CloseContractReportCommand(adminId, reportId, request)), "Contract Report closed.")); }

    [HttpPost("{reportId:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid reportId, [FromBody] DismissContractReportRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new DismissContractReportCommand(adminId, reportId, request)), "Contract Report dismissed.")); }

    [HttpPost("{reportId:guid}/internal-notes")]
    public async Task<IActionResult> AddNote(Guid reportId, [FromBody] AddInternalNoteRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new AddContractReportNoteCommand(adminId, reportId, request.Content)), "Internal note added.")); }

    [HttpPost("{reportId:guid}/link-dispute")]
    public async Task<IActionResult> LinkDispute(Guid reportId, [FromBody] LinkContractReportDisputeRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new LinkContractReportDisputeCommand(adminId, reportId, request)), "Dispute linked.")); }

    [HttpPost("{reportId:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid reportId, [FromBody] AdminEscalateContractReportRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse();
        var report = await Mediator.Send(new GetAdminContractReportDetailQuery(reportId));
        await Mediator.Send(new EscalateReportToDisputeCommand(report.ContractId, reportId, adminId, request.Title, request.Description,
            request.ClaimedAmount, request.RequestedResolution, request.Urgency, true, [], adminId, request.Reason));
        return Ok(ApiResponse<AdminContractReportDetail>.Ok(await Mediator.Send(new GetAdminContractReportDetailQuery(reportId)), "Contract Report escalated to a Dispute."));
    }

    [HttpGet("{reportId:guid}/audit-logs")]
    public async Task<IActionResult> Audit(Guid reportId) => Ok(ApiResponse<IReadOnlyList<AdminContractReportAudit>>.Ok(await Mediator.Send(new GetAdminContractReportAuditQuery(reportId)), "Audit history retrieved."));

    [HttpGet("{reportId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(Guid reportId, Guid attachmentId)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<ContractReportAttachmentDownload>.Ok(await Mediator.Send(new GetContractReportAttachmentDownloadQuery(adminId, reportId, attachmentId)), "Attachment download authorized.")); }
}

public sealed record AddInternalNoteRequest(string Content);
public sealed record AdminEscalateContractReportRequest(string Title, string Description, decimal? ClaimedAmount, string RequestedResolution, DisputeUrgency Urgency, string Reason);
