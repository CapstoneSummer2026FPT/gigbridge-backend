using Application.Common.Models;
using Application.Features.FAQCategories.GetAll.Queries;
using Application.Features.FAQCategories.GetById.Queries;
using Application.Features.FAQCategories.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public;

[ApiController]
[Route("api/faq/categories")]
[AllowAnonymous]
public sealed class FaqCategoryController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAllCategories()
    {
        var result = await Mediator.Send(new GetAllFAQCategoriesQuery());
        return Ok(ApiResponse<IReadOnlyList<FAQCategoryDto>>.Ok(result, "Categories retrieved successfully"));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var result = await Mediator.Send(new GetFAQCategoryByIdQuery(id));

        if (result is null)
            return NotFound(ApiResponse<object>.NotFound("Category not found"));

        return Ok(ApiResponse<FAQCategoryDto>.Ok(result, "Category retrieved successfully"));
    }
}
