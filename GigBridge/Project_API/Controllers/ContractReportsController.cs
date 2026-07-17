using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.ReportContracts.Common.DTOs;
using Application.Features.ReportContracts.Common.Queries;
using Application.Features.ReportContracts.Confirm.Commands;
using Application.Features.ReportContracts.Create.Commands;
using Application.Features.ReportContracts.Respond.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers;

[ApiController]
[Route("api/contracts/{contractId:guid}/reports")]
[Authorize]
public sealed class ContractReportsController : BaseApiController
{
    private const long MaxAttachmentFileSizeBytes = 100 * 1024 * 1024;
    private const long MaxRequestSizeBytes = MaxAttachmentFileSizeBytes * 5 + (2 * 1024 * 1024); // Allow up to 5 attachments + overhead

    /// <summary>
    /// Create a new report/issue for a contract.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> Create(
        Guid contractId,
        [FromForm] int issueType,
        [FromForm] string description,
        [FromForm] string desiredResolution,
        [FromForm] Guid? milestoneId = null)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var attachments = new List<CreateReportFile>();
        foreach (var file in Request.Form.Files)
        {
            var stream = file.OpenReadStream();
            attachments.Add(new CreateReportFile(
                stream,
                file.FileName,
                file.ContentType,
                file.Length));
        }

        var result = await Mediator.Send(new CreateReportCommand(
            contractId,
            userId,
            issueType,
            description,
            desiredResolution,
            milestoneId,
            attachments));

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<ReportContractResponse>.CreatedAt(result, "Report created successfully"));
    }

    /// <summary>
    /// Get all reports for a contract.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractReportsQuery(contractId, userId));

        return Ok(ApiResponse<IReadOnlyList<ReportContractListResponse>>.Ok(result, "Success"));
    }

    /// <summary>
    /// Get report details by ID.
    /// </summary>
    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> GetById(Guid contractId, Guid reportId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetReportByIdQuery(contractId, reportId, userId));

        return Ok(ApiResponse<ReportContractResponse>.Ok(result, "Success"));
    }

    /// <summary>
    /// Respond to a report (respondent only) with optional attachments.
    /// </summary>
    [HttpPost("{reportId:guid}/respond")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> Respond(
        Guid contractId,
        Guid reportId,
        [FromForm] int resolutionAction,
        [FromForm] string? explanation,
        [FromForm] string? proposedResolution,
        [FromForm] string? rejectReason)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var attachments = new List<CreateReportFile>();
        foreach (var file in Request.Form.Files)
        {
            var stream = file.OpenReadStream();
            attachments.Add(new CreateReportFile(
                stream,
                file.FileName,
                file.ContentType,
                file.Length));
        }

        var result = await Mediator.Send(new RespondToReportCommand(
            contractId,
            reportId,
            userId,
            resolutionAction,
            explanation,
            proposedResolution,
            rejectReason,
            attachments));

        return Ok(ApiResponse<ReportContractResponse>.Ok(result, "Response submitted successfully"));
    }

    /// <summary>
    /// Confirm or decline the resolution (reporter only).
    /// </summary>
    [HttpPost("{reportId:guid}/confirm")]
    public async Task<IActionResult> Confirm(
        Guid contractId,
        Guid reportId,
        [FromBody] ConfirmResolutionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ConfirmResolutionCommand(
            contractId,
            reportId,
            userId,
            request.IsAccepted));

        if (request.IsAccepted)
        {
            return Ok(ApiResponse<ReportContractResponse>.Ok(result, "Resolution confirmed successfully"));
        }

        return Ok(ApiResponse<ReportContractResponse>.Ok(result, "Resolution declined. You may escalate to dispute if needed."));
    }
}

/// <summary>
/// Request body for confirming or declining a resolution.
/// </summary>
public sealed record ConfirmResolutionRequest(bool IsAccepted);
