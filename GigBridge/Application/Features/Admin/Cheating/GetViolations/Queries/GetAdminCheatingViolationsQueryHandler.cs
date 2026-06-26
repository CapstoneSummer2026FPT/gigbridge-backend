using Application.Common.Interfaces;
using Application.Features.Admin.Cheating.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Cheating.GetViolations.Queries;

public class GetAdminCheatingViolationsQueryHandler
    : IRequestHandler<GetAdminCheatingViolationsQuery, AdminCheatingViolationsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminCheatingViolationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminCheatingViolationsResponse> Handle(
        GetAdminCheatingViolationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = ApplyFilters(_context.Set<FreelancerCheatingViolation>().AsNoTracking(), request);
        var total = await query.CountAsync(cancellationToken);

        var violations = await query
            .Include(violation => violation.FreelancerUser)
            .Include(violation => violation.ReviewedByAdmin)
            .Include(violation => violation.Proposals)
                .ThenInclude(proposal => proposal.JobPosts)
            .OrderByDescending(violation => violation.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = violations.Select(ToDto).ToList();

        return new AdminCheatingViolationsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    internal static AdminCheatingViolationDto ToDto(FreelancerCheatingViolation violation)
    {
        return new AdminCheatingViolationDto
        {
            FreelancerCheatingViolationId = violation.FreelancerCheatingViolationsId,
            ProposalId = violation.ProposalsId,
            FreelancerUserId = violation.FreelancerUserId,
            FreelancerName = violation.FreelancerUser.FullName,
            FreelancerEmail = violation.FreelancerUser.Email,
            JobPostId = violation.Proposals.JobPostsId,
            JobTitle = violation.Proposals.JobPosts.Title,
            ViolationNumber = violation.ViolationNumber,
            TotalEventCount = violation.TotalEventCount,
            CopyCount = violation.CopyCount,
            PasteCount = violation.PasteCount,
            TabSwitchCount = violation.TabSwitchCount,
            Action = violation.Action,
            EloDelta = violation.EloDelta,
            SuspendedUntil = violation.SuspendedUntil,
            IsReviewed = violation.IsReviewed,
            ReviewedByAdminId = violation.ReviewedByAdminId,
            ReviewedByAdminName = violation.ReviewedByAdmin == null ? null : violation.ReviewedByAdmin.FullName,
            ReviewedAt = violation.ReviewedAt,
            AdminNote = violation.AdminNote,
            CreatedAt = violation.CreatedAt
        };
    }

    private static IQueryable<FreelancerCheatingViolation> ApplyFilters(
        IQueryable<FreelancerCheatingViolation> query,
        GetAdminCheatingViolationsQuery request)
    {
        if (request.Action.HasValue)
        {
            query = query.Where(violation => violation.Action == request.Action.Value);
        }

        if (request.IsReviewed.HasValue)
        {
            query = query.Where(violation => violation.IsReviewed == request.IsReviewed.Value);
        }

        if (request.FreelancerUserId.HasValue)
        {
            query = query.Where(violation => violation.FreelancerUserId == request.FreelancerUserId.Value);
        }

        if (request.ProposalId.HasValue)
        {
            query = query.Where(violation => violation.ProposalsId == request.ProposalId.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(violation => violation.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(violation => violation.CreatedAt <= request.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(violation =>
                violation.FreelancerUser.FullName.ToLower().Contains(keyword) ||
                violation.FreelancerUser.Email.ToLower().Contains(keyword) ||
                violation.Proposals.JobPosts.Title.ToLower().Contains(keyword));
        }

        return query;
    }
}
