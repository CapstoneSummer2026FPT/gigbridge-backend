using Application.DTOs.Admin;
using Application.Common.Models;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/disputes")]
public sealed class AdminDisputesController : AdminControllerBase
{
    private readonly IAdminDisputeService _disputeService;

    public AdminDisputesController(IAdminDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _disputeService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Disputes retrieved successfully"));
    }

    [HttpGet("{disputeId:guid}")]
    public async Task<IActionResult> Get(Guid disputeId, CancellationToken cancellationToken)
    {
        var data = await _disputeService.GetAsync(disputeId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Dispute retrieved successfully"));
    }

    [HttpPatch("{disputeId:guid}/review")]
    public async Task<IActionResult> Review(Guid disputeId, [FromBody] DisputeReviewRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _disputeService.ReviewAsync(disputeId, request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Dispute moved to review successfully"));
    }

    [HttpPatch("{disputeId:guid}/resolution")]
    public async Task<IActionResult> Resolve(Guid disputeId, [FromBody] DisputeResolutionRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _disputeService.ResolveAsync(disputeId, request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Dispute resolution recorded successfully"));
    }
}
