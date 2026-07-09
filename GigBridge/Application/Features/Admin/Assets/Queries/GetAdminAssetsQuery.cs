using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Admin.Assets.Queries;

public sealed record AdminAssetDto(
    Guid AssetId,
    string FileName,
    string FileUrl,
    string? MimeType,
    long? FileSize,
    string AssetType, // "Deliverable" or "MilestoneAttachment"
    Guid ContractId,
    string ContractTitle,
    string UploadedBy,
    Guid UploadedByUserId,
    Guid? JobPostId,
    DateTime CreatedAt);

public sealed record GetAdminAssetsQuery(
    Guid AdminUserId,
    string? Search = null,
    Guid? JobPostId = null,
    Guid? UploadedByUserId = null) : IRequest<IReadOnlyList<AdminAssetDto>>;

public sealed class GetAdminAssetsQueryHandler :
    IRequestHandler<GetAdminAssetsQuery, IReadOnlyList<AdminAssetDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminAssetsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminAssetDto>> Handle(
        GetAdminAssetsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can access platform assets.");
        }

        // Fetch handoffs
        var handoffs = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .Include(h => h.Contract)
            .Include(h => h.SubmittedByUser)
            .ToListAsync(cancellationToken);

        // Fetch milestone attachments
        var milestoneAttachments = await _context.Set<MilestoneAttachment>()
            .AsNoTracking()
            .Include(ma => ma.Milestones)
                .ThenInclude(m => m.Contracts)
            .Include(ma => ma.UploadedByUser)
            .ToListAsync(cancellationToken);

        var list = new List<AdminAssetDto>();

        foreach (var h in handoffs)
        {
            list.Add(new AdminAssetDto(
                h.ContractProductHandoffId,
                h.FileName ?? "Deliverable File",
                h.FileUrl ?? "",
                h.MimeType,
                h.FileSizeBytes,
                "Deliverable",
                h.ContractsId,
                h.Contract?.Title ?? "Contract Details",
                h.SubmittedByUser?.FullName ?? "Unknown",
                h.SubmittedByUserId,
                h.Contract?.JobPostsId,
                h.CreatedAt));
        }

        foreach (var ma in milestoneAttachments)
        {
            list.Add(new AdminAssetDto(
                ma.MilestoneAttachmentsId,
                ma.FileName,
                ma.FileUrl,
                ma.MimeType,
                ma.FileSize,
                "MilestoneAttachment",
                ma.Milestones?.ContractsId ?? Guid.Empty,
                ma.Milestones?.Contracts?.Title ?? "Contract Details",
                ma.UploadedByUser?.FullName ?? "Unknown",
                ma.UploadedByUserId ?? Guid.Empty,
                ma.Milestones?.Contracts?.JobPostsId,
                ma.CreatedAt));
        }

        // Filter by search query if provided
        var resultList = list.AsEnumerable();

        if (request.JobPostId.HasValue)
        {
            resultList = resultList.Where(a => a.JobPostId == request.JobPostId.Value);
        }

        if (request.UploadedByUserId.HasValue)
        {
            resultList = resultList.Where(a => a.UploadedByUserId == request.UploadedByUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            resultList = resultList.Where(a =>
                a.FileName.ToLower().Contains(keyword) ||
                a.ContractTitle.ToLower().Contains(keyword) ||
                a.UploadedBy.ToLower().Contains(keyword));
        }

        return resultList.OrderByDescending(a => a.CreatedAt).ToList();
    }
}
