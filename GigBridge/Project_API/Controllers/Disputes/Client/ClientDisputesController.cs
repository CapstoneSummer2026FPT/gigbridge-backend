using Application.Common.Models;
using Application.Features.Disputes.Client.Create.Commands;
using Application.Features.Disputes.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Disputes.Client;

[ApiController]
[Authorize(Roles = nameof(UserRole.Client))]
[Route("api/disputes")]
public sealed class ClientDisputesController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new CreateDisputeCommand(userId, request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<DisputeDto>.CreatedAt(result, "Dispute created successfully"));
    }
}
