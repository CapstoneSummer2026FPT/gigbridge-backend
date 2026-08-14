using Application.Common.Models;
using Application.Features.Receipts.Common.DTOs;
using Application.Features.Receipts.Download.Queries;
using Application.Features.Receipts.GetMine.Queries;
using Application.Features.Receipts.Prepare.Commands;
using Application.Features.Receipts.Retry.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers;

namespace Project_API.Controllers.Receipts;

[ApiController]
[Route("api")]
[Authorize(Roles = "Client,Freelancer")]
public sealed class ProjectReceiptsController : BaseApiController
{
    [HttpPost("contracts/{contractId:guid}/receipts/prepare")]
    public async Task<IActionResult> Prepare(Guid contractId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new PrepareProjectReceiptsCommand(contractId, userId), cancellationToken);
        return Ok(ApiResponse<ProjectReceiptSummaryResponse>.Ok(result, "Project receipt prepared."));
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new GetMyProjectReceiptsQuery(userId, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PaginatedList<ProjectReceiptSummaryResponse>>.Ok(
            result, "Project receipts retrieved."));
    }

    [HttpGet("receipts/{receiptId:guid}/download")]
    public async Task<IActionResult> Download(Guid receiptId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new DownloadProjectReceiptQuery(receiptId, userId), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("receipts/{receiptId:guid}/retry")]
    public async Task<IActionResult> Retry(Guid receiptId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new RetryProjectReceiptCommand(receiptId, userId), cancellationToken);
        return Ok(ApiResponse<ProjectReceiptSummaryResponse>.Ok(result, "Project receipt retry queued."));
    }
}
