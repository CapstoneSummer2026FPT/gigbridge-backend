using Application.Common.Models;
using Application.Features.Admin.Faqs.Dto;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.Admin;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/faqs")]
public sealed class AdminFaqsController : AdminControllerBase
{
    private readonly IAdminFaqService _faqService;

    public AdminFaqsController(IAdminFaqService faqService)
    {
        _faqService = faqService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] FaqPageQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _faqService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "FAQs retrieved successfully"));
    }

    [HttpGet("{faqId:guid}")]
    public async Task<IActionResult> Get(Guid faqId, CancellationToken cancellationToken)
    {
        var data = await _faqService.GetAsync(faqId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "FAQ retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveFaqRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _faqService.CreateAsync(request, GetActor(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.CreatedAt(data, "FAQ created successfully"));
    }

    [HttpPut("{faqId:guid}")]
    public async Task<IActionResult> Update(Guid faqId, [FromBody] SaveFaqRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _faqService.UpdateAsync(faqId, request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "FAQ updated successfully"));
    }

    [HttpDelete("{faqId:guid}")]
    public async Task<IActionResult> Delete(Guid faqId, CancellationToken cancellationToken)
    {
        await _faqService.DeleteAsync(faqId, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { faqId }, "FAQ deleted successfully"));
    }
}

