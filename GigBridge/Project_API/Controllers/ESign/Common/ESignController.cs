using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;
using Application.Features.ESign.Client.GetDocumentByJobPost.Queries;
using Application.Features.ESign.Client.SubmitSignature.Commands;
using Application.Features.ESign.Client.SubmitSignature.DTOs;
using Application.Features.ESign.Common.GetDocument.Queries;
using Application.Features.ESign.Common.DownloadDocument.Queries;
using Application.Features.ESign.Common.GetDocuments.Queries;
using Application.Features.ESign.Common.GetMySignedDocuments.Queries;
using Application.Features.ESign.Common.GeneratePdf.Commands;
using Application.Features.ESign.Common.SavePdf.Commands;
using Application.Features.ESign.Common.PreviewPdf.Commands;
using Application.Features.ESign.Common.PreviewPdf.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.ESign.Common;

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

        return Ok(ApiResponse<ESignDocumentStatusResponse>.Ok(result, "E-sign document retrieved"));
    }

    [HttpGet("documents/my-signed")]
    public async Task<IActionResult> GetMySignedDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? status = null,
        [FromQuery] string? documentType = null,
        [FromQuery] string? q = null)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new GetMySignedESignDocumentsQuery(userId, page, pageSize, status, documentType, q));

        return Ok(ApiResponse<PaginatedList<ESignDocumentListItemResponse>>.Ok(result, "Signed e-sign documents retrieved"));
    }

    [HttpGet("documents/my")]
    [Authorize(Roles = "Client,Freelancer")]
    public async Task<IActionResult> GetMyDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? status = null,
        [FromQuery] string? q = null)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new GetESignDocumentsQuery(userId, Page: page, PageSize: pageSize, Status: status, Q: q));

        return Ok(ApiResponse<PaginatedList<ESignDocumentListItemResponse>>.Ok(
            result,
            "E-sign documents retrieved"));
    }

    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new DownloadESignDocumentQuery(documentId, userId));
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("documents/{documentId:guid}/pdf")]
    public async Task<IActionResult> GeneratePdf(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GenerateESignPdfCommand(documentId, userId));
        return Ok(ApiResponse<ESignPdfArtifactResponse>.Ok(result, "PDF prepared from the contract template"));
    }

    [HttpPost("documents/{documentId:guid}/pdf/preview-signature")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> PreviewSignaturePdf(
        Guid documentId,
        [FromBody] PreviewESignPdfRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new PreviewESignPdfCommand(
            documentId,
            userId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString()));
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("documents/{documentId:guid}/pdf/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    
    public async Task<IActionResult> SavePdf(
        Guid documentId,
        IFormFile file,
        [FromForm] int signatureCount)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        if (file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.BadRequest("A PDF file is required"));
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, HttpContext.RequestAborted);
        var result = await Mediator.Send(new SaveESignPdfCommand(
            documentId,
            userId,
            stream.ToArray(),
            file.FileName,
            signatureCount));

        return Ok(ApiResponse<ESignPdfArtifactResponse>.Ok(result, "PDF saved"));
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
