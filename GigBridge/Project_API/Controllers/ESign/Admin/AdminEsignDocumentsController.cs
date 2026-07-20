using Application.Common.Models;
using Application.Features.ESign.Common.DeleteDocument.Commands;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.GetDocuments.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/esign-documents")]
[Route("api/admin/AdminEsignDocuments")]
[Authorize(Roles = "Admin")]
public sealed class AdminEsignDocumentsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? status = null,
        [FromQuery] string? q = null)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetESignDocumentsQuery(
            adminUserId,
            AdminScope: true,
            Page: page,
            PageSize: pageSize,
            Status: status,
            Q: q));

        return Ok(ApiResponse<PaginatedList<ESignDocumentListItemResponse>>.Ok(result, "Success"));
    }

    [HttpGet("by-email/{email}")]
    public async Task<IActionResult> GetByEmail(string email)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(ApiResponse<object>.Error(400, "Email is required"));
        }

        var result = await Mediator.Send(new GetESignDocumentsQuery(
            adminUserId,
            AdminScope: true,
            PageSize: 100,
            Q: email));

        return Ok(ApiResponse<IReadOnlyList<ESignDocumentListItemResponse>>.Ok(result.Items, "Success"));
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        await Mediator.Send(new DeleteDraftESignDocumentCommand(documentId, adminUserId));
        return Ok(ApiResponse<object>.Ok(null!, "E-sign document deleted successfully"));
    }
}
