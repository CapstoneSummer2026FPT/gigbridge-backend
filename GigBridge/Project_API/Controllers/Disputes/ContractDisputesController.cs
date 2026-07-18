using Application.Common.Models;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Application.Features.Disputes.Common.Queries;
using Application.Features.Disputes.Evidence.Add.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Disputes;

[ApiController]
[Route("api/contracts/{contractId:guid}/disputes")]
[Authorize]
public sealed class ContractDisputesController : BaseApiController
{
    private const long MaxEvidenceRequestSizeBytes =
        DisputeEvidenceSupport.MaxFileSizeBytes * DisputeEvidenceSupport.MaxFilesPerRequest
        + (2 * 1024 * 1024);

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetContractDisputesQuery(contractId, userId));
        return Ok(ApiResponse<IReadOnlyList<DisputeResponse>>.Ok(result, "Success"));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetActiveDisputeQuery(contractId, userId));
        return Ok(ApiResponse<DisputeResponse?>.Ok(
            result,
            result is not null ? "Active dispute found" : "No active dispute"));
    }

    [HttpGet("{disputeId:guid}")]
    public async Task<IActionResult> GetById(Guid contractId, Guid disputeId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetDisputeByIdQuery(contractId, disputeId, userId));
        return Ok(ApiResponse<DisputeResponse>.Ok(result, "Success"));
    }

    [HttpPost("{disputeId:guid}/evidence")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxEvidenceRequestSizeBytes)]
    public async Task<IActionResult> AddEvidence(Guid contractId, Guid disputeId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var streams = new List<Stream>();
        try
        {
            var files = new List<DisputeEvidenceFile>(Request.Form.Files.Count);
            foreach (var file in Request.Form.Files)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                files.Add(new DisputeEvidenceFile(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length));
            }

            var result = await Mediator.Send(new AddDisputeEvidenceCommand(
                contractId,
                disputeId,
                userId,
                files));

            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<IReadOnlyList<DisputeEvidenceResponse>>.CreatedAt(
                    result,
                    "Evidence uploaded successfully"));
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadEvidence(
        Guid contractId,
        Guid disputeId,
        Guid evidenceId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetDisputeEvidenceDownloadQuery(
            contractId,
            disputeId,
            evidenceId,
            userId));

        return Ok(ApiResponse<DisputeEvidenceDownloadResponse>.Ok(result, "Success"));
    }
}
