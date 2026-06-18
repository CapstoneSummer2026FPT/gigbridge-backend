using Application.Common.Models;
using Application.Features.SavedFreelancers.Client.CheckSavedFreelancer.Queries;
using Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.DTOs;
using Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.Queries;
using Application.Features.SavedFreelancers.Client.SaveFreelancer.Commands;
using Application.Features.SavedFreelancers.Client.UnsaveFreelancer.Commands;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Client;

[ApiController]
[Route("api/SavedFreelancers")]
[Authorize(Roles = nameof(UserRole.Client))]
public class ClientSavedFreelancersController : BaseApiController
{
    [HttpPost("{freelancerProfileId}")]
    public async Task<IActionResult> SaveFreelancer(Guid freelancerProfileId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new SaveFreelancerCommand(userId, freelancerProfileId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(result, "Freelancer saved successfully"));
    }

    [HttpDelete("{freelancerProfileId}")]
    public async Task<IActionResult> UnsaveFreelancer(Guid freelancerProfileId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UnsaveFreelancerCommand(userId, freelancerProfileId);
        await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(true, "Freelancer unsaved successfully"));
    }

    [HttpGet("my-saved-freelancers")]
    public async Task<IActionResult> GetMySavedFreelancers(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMySavedFreelancersQuery(
            userId,
            pageIndex,
            pageSize
        );

        var result = await Mediator.Send(query);

        return Ok(ApiResponse<IEnumerable<SavedFreelancerDto>>.Ok(result, "Success"));
    }

    [HttpGet("{freelancerProfileId}/check")]
    public async Task<IActionResult> CheckSavedFreelancer(Guid freelancerProfileId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new CheckSavedFreelancerQuery(userId, freelancerProfileId);
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<bool>.Ok(result, "Success"));
    }
}