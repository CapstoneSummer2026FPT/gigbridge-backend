using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Admin.Contracts.Queries;
using Application.Features.Admin.Templates.Commands;
using Application.Features.Admin.Templates.Queries;
using Application.Features.Contracts.Common.GetMyContracts.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project_API.Controllers.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project_API.Controllers.Admin;

public sealed record AdminUpdateContractRequest(
    string Title,
    string Description,
    decimal TotalBudget,
    int Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? EsignContractPdfUrl);

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminContractsController : BaseApiController
{
    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts(
        [FromQuery] int? status,
        [FromQuery] Guid? jobPostId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetAdminContractsQuery(adminUserId, status, jobPostId));
        return Ok(ApiResponse<IReadOnlyList<ContractDtoResponse>>.Ok(result, "Success"));
    }

    [HttpPut("contracts/{contractId:guid}")]
    public async Task<IActionResult> UpdateContract(Guid contractId, [FromBody] AdminUpdateContractRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var context = HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();

        var admin = await context.Set<User>().FirstOrDefaultAsync(u => u.UserId == adminUserId);
        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            return Forbid();
        }

        var contract = await context.Set<Contract>().FirstOrDefaultAsync(c => c.ContractsId == contractId);
        if (contract is null)
        {
            return NotFound(ApiResponse<object>.Error(404, "Contract not found"));
        }

        contract.Title = request.Title;
        contract.Description = request.Description;
        contract.TotalBudget = request.TotalBudget;
        contract.Status = request.Status;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.EsignContractPdfUrl = request.EsignContractPdfUrl;
        if (request.Status == (int)ContractStatus.Completed)
        {
            contract.CompletedAt = DateTime.UtcNow;
        }
        contract.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(default);

        return Ok(ApiResponse<Contract>.Ok(contract, "Contract updated successfully"));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetAdminTemplatesQuery(adminUserId));
        return Ok(ApiResponse<IReadOnlyList<EsignTemplateDto>>.Ok(result, "Success"));
    }

    [HttpGet("templates/{templateId:guid}")]
    public async Task<IActionResult> GetTemplateById(Guid templateId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetAdminTemplateByIdQuery(adminUserId, templateId));
        return Ok(ApiResponse<EsignTemplateDto>.Ok(result, "Success"));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateAdminTemplateCommand command)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var finalCommand = new CreateAdminTemplateCommand(
            adminUserId,
            command.Name,
            command.TemplateCode,
            command.HtmlContent,
            command.Version,
            command.PlaceholderSchema,
            command.Description,
            command.IsActive);

        var result = await Mediator.Send(finalCommand);
        return Ok(ApiResponse<Guid>.Ok(result, "Contract template created successfully"));
    }

    [HttpPut("templates/{templateId:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] UpdateAdminTemplateCommand command)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var finalCommand = new UpdateAdminTemplateCommand(
            adminUserId,
            templateId,
            command.Name,
            command.TemplateCode,
            command.HtmlContent,
            command.Version,
            command.PlaceholderSchema,
            command.Description,
            command.IsActive);

        var result = await Mediator.Send(finalCommand);
        return Ok(ApiResponse<bool>.Ok(result, "Contract template updated successfully"));
    }

    [HttpDelete("templates/{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid templateId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new DeleteAdminTemplateCommand(adminUserId, templateId));
        return Ok(ApiResponse<bool>.Ok(result, "Contract template deleted successfully"));
    }
}
