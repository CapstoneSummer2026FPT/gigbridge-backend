using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Admin.Milestones.Commands;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project_API.Controllers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project_API.Controllers.Admin;

public sealed record AdminOverrideMilestoneRequest(
    string Action,
    string? Note);

public sealed record AdminCreateMilestoneRequest(
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int? SortOrder);

public sealed record AdminUpdateMilestoneRequest(
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int Status,
    int? SortOrder);

[ApiController]
[Route("api/admin/milestones")]
[Authorize(Roles = "Admin")]
public sealed class AdminMilestonesController : BaseApiController
{
    [HttpPost("{milestoneId:guid}/override")]
    public async Task<IActionResult> OverrideMilestone(
        Guid milestoneId,
        [FromBody] AdminOverrideMilestoneRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var command = new AdminOverrideMilestoneCommand(
            adminUserId,
            milestoneId,
            request.Action,
            request.Note);

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.Ok(result, $"Milestone override action '{request.Action}' executed successfully."));
    }

    [HttpGet("contract/{contractId:guid}")]
    public async Task<IActionResult> GetContractMilestones(Guid contractId)
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

        var milestones = await context.Set<Milestone>()
            .Where(m => m.ContractsId == contractId)
            .OrderBy(m => m.SortOrder ?? 0)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<IReadOnlyList<Milestone>>.Ok(milestones, "Success"));
    }

    [HttpPost("contract/{contractId:guid}")]
    public async Task<IActionResult> CreateMilestone(Guid contractId, [FromBody] AdminCreateMilestoneRequest request)
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

        var contractExists = await context.Set<Contract>().AnyAsync(c => c.ContractsId == contractId);
        if (!contractExists)
        {
            return NotFound(ApiResponse<object>.Error(404, "Contract not found"));
        }

        var milestone = new Milestone
        {
            MilestonesId = Guid.NewGuid(),
            ContractsId = contractId,
            Title = request.Title,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = (int)MilestoneStatus.Pending,
            SortOrder = request.SortOrder ?? 0,
            CreatedAt = DateTime.UtcNow
        };

        context.Set<Milestone>().Add(milestone);
        await context.SaveChangesAsync(default);

        return Ok(ApiResponse<Milestone>.Ok(milestone, "Milestone created successfully"));
    }

    [HttpPut("{milestoneId:guid}")]
    public async Task<IActionResult> UpdateMilestone(Guid milestoneId, [FromBody] AdminUpdateMilestoneRequest request)
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

        var milestone = await context.Set<Milestone>().FirstOrDefaultAsync(m => m.MilestonesId == milestoneId);
        if (milestone is null)
        {
            return NotFound(ApiResponse<object>.Error(404, "Milestone not found"));
        }

        milestone.Title = request.Title;
        milestone.Amount = request.Amount;
        milestone.DueDate = request.DueDate;
        milestone.Status = request.Status;
        milestone.SortOrder = request.SortOrder;
        milestone.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(default);

        return Ok(ApiResponse<Milestone>.Ok(milestone, "Milestone updated successfully"));
    }

    [HttpDelete("{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestone(Guid milestoneId)
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

        var milestone = await context.Set<Milestone>()
            .Include(m => m.MilestoneAttachments)
            .Include(m => m.PaymentProofs)
            .Include(m => m.EscrowTransactions)
            .FirstOrDefaultAsync(m => m.MilestonesId == milestoneId);

        if (milestone is null)
        {
            return NotFound(ApiResponse<object>.Error(404, "Milestone not found"));
        }

        // Clean up milestone attachments, payment proofs, escrow transactions
        context.Set<MilestoneAttachment>().RemoveRange(milestone.MilestoneAttachments);
        context.Set<PaymentProof>().RemoveRange(milestone.PaymentProofs);
        context.Set<EscrowTransaction>().RemoveRange(milestone.EscrowTransactions);

        // Nullify reference in wallet transactions
        var walletTxns = await context.Set<WalletTransaction>()
            .Where(wt => wt.MilestonesId == milestoneId)
            .ToListAsync();
        foreach (var wt in walletTxns)
        {
            wt.MilestonesId = null;
        }

        context.Set<Milestone>().Remove(milestone);
        await context.SaveChangesAsync(default);

        return Ok(ApiResponse<bool>.Ok(true, "Milestone deleted successfully"));
    }
}
