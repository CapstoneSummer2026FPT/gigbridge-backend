using Application.Common.Models;
using Application.Features.Elo.Commands.CancelEloAppeal;
using Application.Features.Elo.Commands.CreateEloAppeal;
using Application.Features.Elo.Commands.UploadEloAppealEvidence;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Application.Features.Elo.Queries.GetEloAppealDetail;
using Application.Features.Elo.Queries.GetEloHistory;
using Application.Features.Elo.Queries.GetEloSummary;
using Application.Features.Elo.Queries.GetEloTransactionDetail;
using Application.Features.Elo.Queries.GetMyEloAppeals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Elo;

[ApiController]
[Route("api/elo")]
[Authorize]
public sealed class EloController : BaseApiController
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetEloSummaryQuery(userId));
        return Ok(ApiResponse<EloSummaryDto>.Ok(result, "Elo summary retrieved successfully."));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? filter = null)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetEloHistoryQuery(userId, page, pageSize, filter));
        return Ok(ApiResponse<PaginatedList<EloTransactionDto>>.Ok(result, "Elo history retrieved successfully."));
    }

    [HttpGet("history/{transactionId:guid}")]
    public async Task<IActionResult> GetTransactionDetail(Guid transactionId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetEloTransactionDetailQuery(transactionId, userId));
        return Ok(ApiResponse<EloTransactionDetailDto>.Ok(result, "Elo transaction retrieved successfully."));
    }

    [HttpGet("appeals")]
    public async Task<IActionResult> GetMyAppeals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? status = null)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetMyEloAppealsQuery(userId, page, pageSize, status));
        return Ok(ApiResponse<PaginatedList<EloAppealDto>>.Ok(result, "Elo appeals retrieved successfully."));
    }

    [HttpGet("appeals/{appealId:guid}")]
    public async Task<IActionResult> GetAppealDetail(Guid appealId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetEloAppealDetailQuery(appealId, userId));
        return Ok(ApiResponse<EloAppealDetailDto>.Ok(result, "Elo appeal retrieved successfully."));
    }

    [HttpPost("appeals")]
    [RequestSizeLimit(502 * 1024 * 1024)]
    public async Task<IActionResult> CreateAppeal(
        [FromForm] Guid transactionId,
        [FromForm] string reason,
        [FromForm] List<IFormFile>? files)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var streams = new List<Stream>();
        try
        {
            var appealFiles = new List<EloAppealFile>();
            foreach (var file in files ?? [])
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                appealFiles.Add(new EloAppealFile(stream, file.FileName, file.ContentType, file.Length, null));
            }

            var result = await Mediator.Send(new CreateEloAppealCommand(userId, transactionId, reason, appealFiles));
            return Ok(ApiResponse<EloAppealDto>.Ok(result, "Elo appeal submitted successfully."));
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }

    [HttpPost("appeals/{appealId:guid}/evidence")]
    [RequestSizeLimit(502 * 1024 * 1024)]
    public async Task<IActionResult> UploadAppealEvidence(
        Guid appealId,
        [FromForm] List<IFormFile>? files)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var streams = new List<Stream>();
        try
        {
            var appealFiles = new List<EloAppealFile>();
            foreach (var file in files ?? [])
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                appealFiles.Add(new EloAppealFile(stream, file.FileName, file.ContentType, file.Length, null));
            }

            var result = await Mediator.Send(new UploadEloAppealEvidenceCommand(userId, appealId, appealFiles));
            return Ok(ApiResponse<IReadOnlyList<EloAppealEvidenceDto>>.Ok(result, "Evidence uploaded successfully."));
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }

    [HttpPost("appeals/{appealId:guid}/cancel")]
    public async Task<IActionResult> CancelAppeal(Guid appealId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new CancelEloAppealCommand(userId, appealId));
        return Ok(ApiResponse<EloAppealDto>.Ok(result, "Elo appeal cancelled successfully."));
    }
}
