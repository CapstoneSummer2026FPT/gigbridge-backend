using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.GetMyContracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.GetMyContracts.Queries;

public class GetMyContractsQueryHandler : IRequestHandler<GetMyContractsQuery, List<ContractDtoResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetMyContractsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContractDtoResponse>> Handle(GetMyContractsQuery request, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.UserId == request.UserId, cancellationToken);

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(fp => fp.UserId == request.UserId, cancellationToken);

        if (clientProfile == null && freelancerProfile == null)
        {
            return new List<ContractDtoResponse>();
        }

        var query = _context.Set<Contract>()
            .AsNoTracking()
            .Include(c => c.ClientProfiles)
                .ThenInclude(cp => cp!.User)
            .Include(c => c.FreelancerProfiles)
                .ThenInclude(fp => fp!.User)
            .AsQueryable();

        if (clientProfile != null && freelancerProfile != null)
        {
            query = query.Where(c => c.ClientProfilesId == clientProfile.ClientProfilesId || 
                                    (c.FreelancerProfilesId.HasValue && c.FreelancerProfilesId.Value == freelancerProfile.FreelancerProfilesId));
        }
        else if (clientProfile != null)
        {
            query = query.Where(c => c.ClientProfilesId == clientProfile.ClientProfilesId);
        }
        else
        {
            query = query.Where(c => c.FreelancerProfilesId.HasValue && freelancerProfile != null && c.FreelancerProfilesId.Value == freelancerProfile.FreelancerProfilesId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.Status == request.Status.Value);
        }

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var contractIds = contracts.Select(c => c.ContractsId).ToList();
        var reviewedContractIds = (await _context.Set<Review>()
            .AsNoTracking()
            .Where(review =>
                contractIds.Contains(review.ContractsId) &&
                review.ReviewerId == request.UserId)
            .Select(review => review.ContractsId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

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
            ClientUserId = c.ClientProfiles?.User?.UserId,
            FreelancerUserId = c.FreelancerProfiles?.User?.UserId,
            HasReviewedByCurrentUser = reviewedContractIds.Contains(c.ContractsId),
            CanReview = c.Status == (int)ContractStatus.Completed &&
                !reviewedContractIds.Contains(c.ContractsId)
        }).ToList();
    }
}
