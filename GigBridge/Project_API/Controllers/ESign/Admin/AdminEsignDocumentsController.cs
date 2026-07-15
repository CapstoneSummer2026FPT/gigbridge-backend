using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.ESign.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_API.Controllers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public sealed class AdminEsignDocumentsController : BaseApiController
{
    private const string JobDocumentType = "JobPost";
    private const string ContractDocumentType = "Contract";

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var context = HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();

        var admin = await context.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminUserId);
        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            return Forbid();
        }

        var query =
            from document in context.Set<EsignDocument>().AsNoTracking()
            join jobPost in context.Set<JobPost>().AsNoTracking()
                on document.JobPostsId equals jobPost.JobPostsId
            join contract in context.Set<Contract>().AsNoTracking()
                on document.ContractsId equals contract.ContractsId into contractJoin
            from contract in contractJoin.DefaultIfEmpty()
            select new
            {
                document.EsignDocumentsId,
                document.JobPostsId,
                document.ContractsId,
                document.DocumentCode,
                DocumentType = document.ContractsId.HasValue ? ContractDocumentType : JobDocumentType,
                Title = document.ContractsId.HasValue && contract != null ? contract.Title : jobPost.Title,
                document.Status,
                document.FinalizedAt,
                document.ExportedPdfUrl,
                document.CreatedAt,
                document.UpdatedAt
            };

        var list = await query.ToListAsync();

        var result = list.Select(x => new ESignDocumentListItemResponse(
            x.EsignDocumentsId,
            x.JobPostsId,
            x.ContractsId,
            x.DocumentCode,
            x.DocumentType,
            x.Title,
            x.Status,
            0,
            null,
            context.Set<EsignSignature>().Any(s => s.EsignDocumentsId == x.EsignDocumentsId && s.SignerRole == 0 && s.Status == 1),
            context.Set<EsignSignature>().Any(s => s.EsignDocumentsId == x.EsignDocumentsId && s.SignerRole == 1 && s.Status == 1),
            context.Set<EsignSignature>().Count(s => s.EsignDocumentsId == x.EsignDocumentsId && s.Status == 1),
            x.FinalizedAt,
            x.ExportedPdfUrl,
            x.CreatedAt,
            x.UpdatedAt
        )).ToList();

        return Ok(ApiResponse<IReadOnlyList<ESignDocumentListItemResponse>>.Ok(result, "Success"));
    }

    [HttpGet("by-email/{email}")]
    public async Task<IActionResult> GetByEmail(string email)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var context = HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();

        var admin = await context.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminUserId);
        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(ApiResponse<object>.Error(400, "Email is required"));
        }

        var searchEmail = email.Trim().ToLowerInvariant();

        var query =
            from document in context.Set<EsignDocument>().AsNoTracking()
            join jobPost in context.Set<JobPost>().AsNoTracking()
                on document.JobPostsId equals jobPost.JobPostsId
            join clientProfile in context.Set<ClientProfile>().AsNoTracking()
                on jobPost.ClientProfilesId equals clientProfile.ClientProfilesId
            join clientUser in context.Set<User>().AsNoTracking()
                on clientProfile.UserId equals clientUser.UserId
            join contract in context.Set<Contract>().AsNoTracking()
                on document.ContractsId equals contract.ContractsId into contractJoin
            from contract in contractJoin.DefaultIfEmpty()
            join freelancerProfile in context.Set<FreelancerProfile>().AsNoTracking()
                on (contract != null ? contract.FreelancerProfilesId : (Guid?)null) equals freelancerProfile.FreelancerProfilesId into freelancerJoin
            from freelancerProfile in freelancerJoin.DefaultIfEmpty()
            join freelancerUser in context.Set<User>().AsNoTracking()
                on (freelancerProfile != null ? freelancerProfile.UserId : (Guid?)null) equals freelancerUser.UserId into freelancerUserJoin
            from freelancerUser in freelancerUserJoin.DefaultIfEmpty()
            where context.Set<EsignSignature>().Any(s => s.EsignDocumentsId == document.EsignDocumentsId && s.User.Email.ToLower() == searchEmail) ||
                  clientUser.Email.ToLower() == searchEmail ||
                  (freelancerUser != null && freelancerUser.Email.ToLower() == searchEmail)
            select new
            {
                document.EsignDocumentsId,
                document.JobPostsId,
                document.ContractsId,
                document.DocumentCode,
                DocumentType = document.ContractsId.HasValue ? ContractDocumentType : JobDocumentType,
                Title = document.ContractsId.HasValue && contract != null ? contract.Title : jobPost.Title,
                document.Status,
                document.FinalizedAt,
                document.ExportedPdfUrl,
                document.CreatedAt,
                document.UpdatedAt
            };

        var list = await query.ToListAsync();

        var result = list.Select(x => new ESignDocumentListItemResponse(
            x.EsignDocumentsId,
            x.JobPostsId,
            x.ContractsId,
            x.DocumentCode,
            x.DocumentType,
            x.Title,
            x.Status,
            0,
            null,
            context.Set<EsignSignature>().Any(s => s.EsignDocumentsId == x.EsignDocumentsId && s.SignerRole == 0 && s.Status == 1),
            context.Set<EsignSignature>().Any(s => s.EsignDocumentsId == x.EsignDocumentsId && s.SignerRole == 1 && s.Status == 1),
            context.Set<EsignSignature>().Count(s => s.EsignDocumentsId == x.EsignDocumentsId && s.Status == 1),
            x.FinalizedAt,
            x.ExportedPdfUrl,
            x.CreatedAt,
            x.UpdatedAt
        )).ToList();

        return Ok(ApiResponse<IReadOnlyList<ESignDocumentListItemResponse>>.Ok(result, "Success"));
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var context = HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();

        var admin = await context.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminUserId);
        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            return Forbid();
        }

        var document = await context.Set<EsignDocument>()
            .FirstOrDefaultAsync(d => d.EsignDocumentsId == documentId);

        if (document == null)
        {
            return NotFound(ApiResponse<object>.Error(404, "E-sign document not found"));
        }

        var signatures = await context.Set<EsignSignature>()
            .Where(s => s.EsignDocumentsId == documentId)
            .ToListAsync();

        context.Set<EsignSignature>().RemoveRange(signatures);
        context.Set<EsignDocument>().Remove(document);

        await context.SaveChangesAsync(default);

        return Ok(ApiResponse<object>.Ok(null, "E-sign document deleted successfully"));
    }
}
