using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Queries;
using Application.Features.Disputes.Create.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Disputes;

[ApiController]
[Route("api/contracts/{contractId:guid}/disputes")]
[Authorize]
public sealed class ContractDisputesController : BaseApiController
{
    private const long MaxEvidenceFileSizeBytes = 100 * 1024 * 1024;
    private const long MaxRequestSizeBytes = MaxEvidenceFileSizeBytes + (1024 * 1024);

    /// <summary>
    /// Tạo dispute mới cho contract.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> Create(
        Guid contractId,
        [FromForm] string reason,
        [FromForm] Guid? milestoneId = null,
        [FromForm] string? evidenceDescription = null,
        IFormFile? evidence = null)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        if (Request.Form.Files.Count > 1)
        {
            throw new BadRequestException("Only one evidence file can be uploaded at a time.");
        }

        if (Request.Form.Files.Count == 1 && evidence is null)
        {
            throw new BadRequestException("The uploaded file must use the 'evidence' form field.");
        }

        using var evidenceStream = evidence?.OpenReadStream();
        CreateDisputeFile? evidenceFile = null;
        if (evidence is not null && evidenceStream is not null)
        {
            evidenceFile = new CreateDisputeFile(
                evidenceStream,
                evidence.FileName,
                evidence.ContentType,
                evidence.Length);
        }

        var result = await Mediator.Send(new CreateDisputeCommand(
            contractId,
            userId,
            reason,
            milestoneId,
            evidenceFile,
            evidenceDescription));

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<DisputeResponse>.CreatedAt(result, "Dispute created successfully"));
    }

    /// <summary>
    /// Lấy danh sách dispute của contract.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractDisputesQuery(contractId, userId));

        return Ok(ApiResponse<IReadOnlyList<DisputeResponse>>.Ok(result, "Success"));
    }

    /// <summary>
    /// Lấy active dispute (Open hoặc UnderReview) của contract, nếu có.
    /// Trả về 200 với data = null nếu không có active dispute.
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetActiveDisputeQuery(contractId, userId));

        return Ok(ApiResponse<DisputeResponse?>.Ok(result,
            result is not null ? "Active dispute found" : "No active dispute"));
    }

    /// <summary>
    /// Lấy chi tiết dispute theo ID.
    /// </summary>
    [HttpGet("{disputeId:guid}")]
    public async Task<IActionResult> GetById(Guid contractId, Guid disputeId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetDisputeByIdQuery(contractId, disputeId, userId));

        return Ok(ApiResponse<DisputeResponse>.Ok(result, "Success"));
    }

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadEvidence(
        Guid contractId,
        Guid disputeId,
        Guid evidenceId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetDisputeEvidenceDownloadQuery(
            contractId,
            disputeId,
            evidenceId,
            userId));

        return Ok(ApiResponse<DisputeEvidenceDownloadResponse>.Ok(result, "Success"));
    }
}
