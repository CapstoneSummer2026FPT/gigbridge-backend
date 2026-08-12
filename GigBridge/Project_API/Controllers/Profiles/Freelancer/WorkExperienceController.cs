using Application.Common.Models;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.WorkExperiences.Common.DTOs;
using Application.Features.WorkExperiences.CreateWorkExperience.Commands;
using Application.Features.WorkExperiences.DeleteWorkExperience.Commands;
using Application.Features.WorkExperiences.GetWorkExperiences.Queries;
using Application.Features.WorkExperiences.UpdateWorkExperience.Commands;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Profiles.Freelancer;

[ApiController]
[Authorize]
[Route("api/work-experience")]
public sealed class WorkExperienceController : BaseApiController
{
    [HttpGet("me")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetMyWorkExperiences()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetWorkExperiencesQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<WorkExperienceDto>>.Ok(result, "Success"));
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetWorkExperiences(Guid userId)
    {
        var result = await Mediator.Send(new GetWorkExperiencesQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<WorkExperienceDto>>.Ok(result, "Success"));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> CreateWorkExperience([FromBody] WorkExperienceInputDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateWorkExperienceCommand(userId, dto));
        return Ok(ApiResponse<WorkExperienceDto>.Ok(result, "Work experience created successfully"));
    }

    [HttpPut("{workExperienceId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> UpdateWorkExperience(
        Guid workExperienceId,
        [FromBody] WorkExperienceInputDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new UpdateWorkExperienceCommand(userId, workExperienceId, dto));
        return Ok(ApiResponse<WorkExperienceDto>.Ok(result, "Work experience updated successfully"));
    }

    [HttpDelete("{workExperienceId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> DeleteWorkExperience(Guid workExperienceId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new DeleteWorkExperienceCommand(userId, workExperienceId));
        return Ok(ApiResponse<bool>.Ok(result, "Work experience deleted successfully"));
    }
}
