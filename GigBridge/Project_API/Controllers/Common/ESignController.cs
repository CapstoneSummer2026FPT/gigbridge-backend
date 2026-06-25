using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;
using Application.Features.ESign.Client.GetDocumentByJobPost.Queries;
using Application.Features.ESign.Client.SubmitSignature.Commands;
using Application.Features.ESign.Client.SubmitSignature.DTOs;
using Application.Features.ESign.Common.GetDocument.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/ESign")]
[Authorize]
public sealed class ESignController : BaseApiController
{
    [HttpGet("documents/{documentId:guid}")]
    public async Task<IActionResult> GetDocument(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetESignDocumentQuery(documentId, userId));

        return Ok(ApiResponse<ESignDocumentResponse>.Ok(result, "E-sign document retrieved"));
    }

    [HttpGet("documents/by-job/{jobPostId:guid}")]
    public async Task<IActionResult> GetDocumentByJob(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetESignDocumentByJobPostQuery(jobPostId, userId));

        return Ok(ApiResponse<ESignDocumentResponse>.Ok(result, "E-sign document retrieved"));
    }

    [HttpGet("documents/by-contract/{contractId:guid}")]
    public async Task<IActionResult> GetDocumentByContract(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.ESign.Common.GetDocumentByContract.Queries.GetESignDocumentByContractQuery(contractId, userId));

        return Ok(ApiResponse<ESignDocumentResponse>.Ok(result, "E-sign document retrieved"));
    }

    [HttpPost("documents/from-job/{jobPostId:guid}")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CreateDocumentFromJob(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateESignDocumentFromJobPostCommand(jobPostId, userId));

        return Ok(ApiResponse<ESignDocumentResponse>.Ok(result, "E-sign document prepared"));
    }

    [HttpPost("signatures")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> SubmitSignature([FromBody] SubmitESignSignatureRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new SubmitESignSignatureCommand(
                userId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()));

        return Ok(ApiResponse<ESignSignatureResponse>.Ok(result, "E-sign document signed"));
    }
}
