using Application.Common.Models;
using Application.Features.FAQs.GetAll.Queries;
using Application.Features.FAQs.GetById.Queries;
using Application.Features.FAQs.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public;

[ApiController]
[Route("api/faq")]
[AllowAnonymous]
public sealed class FaqController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAllFAQs([FromQuery] int? categoryId = null)
    {
        var result = await Mediator.Send(new GetAllFAQsQuery(categoryId));
        return Ok(ApiResponse<IReadOnlyList<FAQDto>>.Ok(result, "FAQs retrieved successfully"));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFAQById(int id)
    {
        var result = await Mediator.Send(new GetFAQByIdQuery(id));

        if (result is null)
            return NotFound(ApiResponse<object>.NotFound("FAQ not found"));

        return Ok(ApiResponse<FAQDto>.Ok(result, "FAQ retrieved successfully"));
    }
}
