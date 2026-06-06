using Application.Common.Models;
using Application.Features.Admin.FAQCategories.Create.Commands;
using Application.Features.Admin.FAQCategories.Create.DTOs;
using Application.Features.Admin.FAQCategories.Delete.Commands;
using Application.Features.Admin.FAQCategories.GetAll.Queries;
using Application.Features.Admin.FAQCategories.ToggleActivity.Commands;
using Application.Features.Admin.FAQCategories.Update.Commands;
using Application.Features.Admin.FAQCategories.Update.DTOs;
using Application.Features.FAQCategories.Shared.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.FAQ;

[Route("api/admin/faq/categories")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminFAQCategoryController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAllCategories()
    {
        var result = await Mediator.Send(new GetAllAdminFAQCategoriesQuery());
        return Ok(ApiResponse<IReadOnlyList<FAQCategoryDto>>.Ok(result, "Categories retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateFAQCategoryRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var result = await Mediator.Send(new CreateFAQCategoryCommand(request));
        return Ok(ApiResponse<FAQCategoryDto>.Ok(result, "Category created successfully"));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateFAQCategoryRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var result = await Mediator.Send(new UpdateFAQCategoryCommand(id, request));

        if (result is null)
            return NotFound(ApiResponse<object>.NotFound("Category not found"));

        return Ok(ApiResponse<FAQCategoryDto>.Ok(result, "Category updated successfully"));
    }

    [HttpPatch("{id:int}/toggle-activity")]
    public async Task<IActionResult> ToggleCategoryActivity(int id)
    {
        var result = await Mediator.Send(new ToggleFAQCategoryActivityCommand(id));

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("Category not found"));

        return Ok(ApiResponse<object>.NoContent("Category activity toggled successfully"));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await Mediator.Send(new DeleteFAQCategoryCommand(id));

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("Category not found"));

        return Ok(ApiResponse<object>.NoContent("Category deleted successfully"));
    }
}
