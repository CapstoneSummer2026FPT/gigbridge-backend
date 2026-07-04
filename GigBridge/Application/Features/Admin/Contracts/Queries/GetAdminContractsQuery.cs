using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.GetMyContracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Admin.Contracts.Queries;

public sealed record GetAdminContractsQuery(
    Guid AdminUserId,
    int? Status = null,
    Guid? JobPostId = null) : IRequest<IReadOnlyList<ContractDtoResponse>>;

public sealed class GetAdminContractsQueryHandler :
    IRequestHandler<GetAdminContractsQuery, IReadOnlyList<ContractDtoResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminContractsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ContractDtoResponse>> Handle(
        GetAdminContractsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can access platform contracts.");
        }

        var query = _context.Set<Contract>()
            .AsNoTracking()
            .Include(c => c.ClientProfiles)
                .ThenInclude(cp => cp!.User)
            .Include(c => c.FreelancerProfiles)
                .ThenInclude(fp => fp!.User)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.Status == request.Status.Value);
        }

        if (request.JobPostId.HasValue)
        {
            query = query.Where(c => c.JobPostsId == request.JobPostId.Value);
        }

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return contracts.Select(c => new ContractDtoResponse
        {
            ContractsId = c.ContractsId,
            JobPostsId = c.JobPostsId,
            ClientProfilesId = c.ClientProfilesId,
            FreelancerProfilesId = c.FreelancerProfilesId,
            ProposalsId = c.ProposalsId,
            Title = c.Title,
            Description = c.Description,
            TotalBudget = c.TotalBudget,
            Status = c.Status,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            CompletedAt = c.CompletedAt,
            EsignContractPdfUrl = c.EsignContractPdfUrl,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            ClientName = c.ClientProfiles?.User?.FullName ?? "Client",
            FreelancerName = c.FreelancerProfiles?.User?.FullName,
            HasReviewedByCurrentUser = false,
            CanReview = false
        }).ToList();
    }
}
