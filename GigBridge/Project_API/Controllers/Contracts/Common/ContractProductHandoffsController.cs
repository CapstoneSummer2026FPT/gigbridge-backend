using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Contracts.ProductHandoffs.Acknowledge.Commands;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Application.Features.Contracts.ProductHandoffs.Download.Queries;
using Application.Features.Contracts.ProductHandoffs.GetList.Queries;
using Application.Features.Contracts.ProductHandoffs.Submit.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Contracts.Common;

[ApiController]
[Route("api/contracts/{contractId:guid}/product-handoffs")]
[Authorize]
public sealed class ContractProductHandoffsController : BaseApiController
{
    private const long MaxRequestSizeBytes = 100 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> GetHandoffs(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractProductHandoffsQuery(contractId, userId));

        return Ok(ApiResponse<IReadOnlyList<ContractProductHandoffResponse>>.Ok(result, "Success"));
    }

    [HttpPost]
    [Authorize(Roles = "Client")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> Submit(Guid contractId, [FromForm] string? externalUrl, [FromForm] string? note)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        if (Request.Form.Files.Count > 1)
        {
            throw new BadRequestException("Only one product file can be submitted at a time.");
        }

        SubmitContractProductHandoffFile? commandFile = null;
        var file = Request.Form.Files.FirstOrDefault();
        if (file is not null)
        {
            commandFile = new SubmitContractProductHandoffFile(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length);
        }

        var result = await Mediator.Send(
            new SubmitContractProductHandoffCommand(contractId, userId, commandFile, externalUrl, note));

        return Ok(ApiResponse<ContractProductHandoffResponse>.Ok(result, "Product materials submitted"));
    }

    [HttpPost("{handoffId:guid}/acknowledge")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> Acknowledge(Guid contractId, Guid handoffId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new AcknowledgeContractProductHandoffCommand(contractId, handoffId, userId));

        return Ok(ApiResponse<ContractProductHandoffResponse>.Ok(result, "Product materials acknowledged"));
    }

    [HttpGet("{handoffId:guid}/download")]
    public async Task<IActionResult> Download(Guid contractId, Guid handoffId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractProductHandoffDownloadQuery(contractId, handoffId, userId));

        return Ok(ApiResponse<ProductHandoffDownloadResponse>.Ok(result, "Success"));
    }
}
