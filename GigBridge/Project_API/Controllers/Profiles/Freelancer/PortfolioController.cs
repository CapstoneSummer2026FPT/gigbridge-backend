using Application.Common.Models;
using Application.Features.Portfolios.Common.DTOs;
using Application.Features.Portfolios.CreatePortfolioItem.Commands;
using Application.Features.Portfolios.DeletePortfolioItem.Commands;
using Application.Features.Portfolios.GetPortfolioItems.Queries;
using Application.Features.Portfolios.UpdatePortfolioItem.Commands;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Profiles.Freelancer;

[ApiController]
[Authorize]
[Route("api/portfolio")]
public sealed class PortfolioController : BaseApiController
{
    [HttpGet("me")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetMyPortfolio()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetPortfolioItemsQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<PortfolioItemDto>>.Ok(result, "Success"));
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetPortfolio(Guid userId)
    {
        var result = await Mediator.Send(new GetPortfolioItemsQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<PortfolioItemDto>>.Ok(result, "Success"));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> CreatePortfolioItem([FromBody] PortfolioItemInputDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreatePortfolioItemCommand(userId, dto));
        return Ok(ApiResponse<PortfolioItemDto>.Ok(result, "Portfolio item created successfully"));
    }

    [HttpPut("{portfolioItemId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> UpdatePortfolioItem(
        Guid portfolioItemId,
        [FromBody] PortfolioItemInputDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new UpdatePortfolioItemCommand(userId, portfolioItemId, dto));
        return Ok(ApiResponse<PortfolioItemDto>.Ok(result, "Portfolio item updated successfully"));
    }

    [HttpDelete("{portfolioItemId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> DeletePortfolioItem(Guid portfolioItemId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new DeletePortfolioItemCommand(userId, portfolioItemId));
        return Ok(ApiResponse<bool>.Ok(result, "Portfolio item deleted successfully"));
    }
}
