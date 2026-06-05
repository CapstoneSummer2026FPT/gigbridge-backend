using Application.Common.Models;
using Application.Features.Admin.FAQs.Create.Commands;
using Application.Features.Admin.FAQs.Create.DTOs;
using Application.Features.Admin.FAQs.Delete.Commands;
using Application.Features.Admin.FAQs.GetAll.Queries;
using Application.Features.Admin.FAQs.ToggleActivity.Commands;
using Application.Features.Admin.FAQs.Update.Commands;
using Application.Features.Admin.FAQs.Update.DTOs;
using Application.Features.FAQs.Shared.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.FAQ;

[Route("api/admin/faq")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminFAQController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAllFAQs([FromQuery] int? categoryId = null)
    {
        var result = await Mediator.Send(new GetAllAdminFAQsQuery(categoryId));
        return Ok(ApiResponse<IReadOnlyList<FAQDto>>.Ok(result, "FAQs retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFAQ([FromBody] CreateFAQRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var result = await Mediator.Send(new CreateFAQCommand(request));
        return Ok(ApiResponse<FAQDto>.Ok(result, "FAQ created successfully"));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFAQ(int id, [FromBody] UpdateFAQRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var result = await Mediator.Send(new UpdateFAQCommand(id, request));

        if (result is null)
            return NotFound(ApiResponse<object>.NotFound("FAQ not found"));

        return Ok(ApiResponse<FAQDto>.Ok(result, "FAQ updated successfully"));
    }

    [HttpPatch("{id:int}/toggle-activity")]
    public async Task<IActionResult> ToggleFAQActivity(int id)
    {
        var result = await Mediator.Send(new ToggleFAQActivityCommand(id));

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("FAQ not found"));

        return Ok(ApiResponse<object>.NoContent("FAQ activity toggled successfully"));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFAQ(int id)
    {
        var result = await Mediator.Send(new DeleteFAQCommand(id));

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("FAQ not found"));

        return Ok(ApiResponse<object>.NoContent("FAQ deleted successfully"));
    }
}
