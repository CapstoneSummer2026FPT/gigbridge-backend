using Application.Common.Interfaces;
using Application.Features.Proposals.DTOs;
using Application.Features.Proposals.GetAllProposals.Queries;
using Application.Features.Proposals.GetMyProposals.Queries;
using Application.Features.Proposals.GetProposalsByJobPost.Queries;
using Application.Features.Proposals.Services;
using Application.Features.Proposals.SubmitProposal.Commands;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.Proposals;

public class ProposalsService : IProposalsService
{
    private readonly IApplicationDbContext _context;

    public ProposalsService(IApplicationDbContext context)
    {
        _context = context;
    }

    #region Submit Proposal
    public async Task<Guid> SubmitProposalAsync(
    SubmitProposalCommand command,
    CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(x => x.UserId == command.UserId, cancellationToken);

        if (freelancerProfile == null)
            throw new Exception("Freelancer profile không tồn tại.");

        var jobPost = await _context.Set<JobPost>()
            .FirstOrDefaultAsync(j => j.JobPostsId == request.JobPostsId, cancellationToken);

        if (jobPost == null)
            throw new Exception("Job Post không tồn tại.");

        if (jobPost.Status != 1)
            throw new Exception("Job Post này hiện không nhận thêm Proposal.");

        bool hasSubmitted = await _context.Set<Proposal>()
            .AnyAsync(p => p.JobPostsId == request.JobPostsId
                        && p.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId,
                cancellationToken);

        if (hasSubmitted)
            throw new Exception("Bạn đã gửi Proposal cho công việc này rồi.");

        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = request.JobPostsId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            CoverLetter = request.CoverLetter,
            ProposedRate = request.ProposedRate,
            ProposedDuration = request.ProposedDuration,
            Status = 0,
            SubmittedAt = DateTime.UtcNow
        };

        _context.Set<Proposal>().Add(proposal);
        await _context.SaveChangesAsync(cancellationToken);

        return proposal.ProposalsId;
    }
    #endregion

    #region Get My Proposals (Freelancer)
    public async Task<IEnumerable<ProposalDto>> GetMyProposalsAsync(
    GetMyProposalsQuery request,
    CancellationToken cancellationToken = default)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);

        if (freelancerProfile == null)
            throw new Exception("Freelancer profile không tồn tại.");

        var proposals = await _context.Set<Proposal>()
            .Include(p => p.JobPosts)
            .Where(p => p.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId)
            .OrderByDescending(p => p.SubmittedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return proposals.Select(p => new ProposalDto
        {
            ProposalsId = p.ProposalsId,
            JobPostsId = p.JobPostsId,
            JobTitle = p.JobPosts?.Title ?? "",
            FreelancerProfilesId = p.FreelancerProfilesId,
            CoverLetter = p.CoverLetter ?? "",
            ProposedRate = p.ProposedRate ?? 0m,
            ProposedDuration = p.ProposedDuration ?? "",
            Status = p.Status,
            SubmittedAt = p.SubmittedAt ?? DateTime.UtcNow
        }).ToList();
    }
    #endregion

    #region Get Proposals By JobPost (Client)
    public async Task<IEnumerable<ProposalDto>> GetProposalsByJobPostAsync(
    GetProposalsByJobPostQuery request,
    CancellationToken cancellationToken = default)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);

        if (clientProfile == null)
            throw new Exception("Client profile không tồn tại.");

        var jobPost = await _context.Set<JobPost>()
            .FirstOrDefaultAsync(j => j.JobPostsId == request.JobPostsId, cancellationToken);

        if (jobPost == null)
            throw new Exception("Job post không tồn tại.");

        if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
            throw new Exception("Bạn không có quyền xem proposal của job này.");

        var proposals = await _context.Set<Proposal>()
            .Include(p => p.JobPosts)
            .Include(p => p.FreelancerProfiles)
                .ThenInclude(fp => fp.User)
            .Where(p => p.JobPostsId == request.JobPostsId)
            .OrderByDescending(p => p.SubmittedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return proposals.Select(p => new ProposalDto
        {
            ProposalsId = p.ProposalsId,
            JobPostsId = p.JobPostsId,
            JobTitle = jobPost.Title,
            FreelancerProfilesId = p.FreelancerProfilesId,
            FreelancerName = p.FreelancerProfiles?.User?.FullName ?? "",
            CoverLetter = p.CoverLetter ?? "",
            ProposedRate = p.ProposedRate ?? 0m,
            ProposedDuration = p.ProposedDuration ?? "",
            Status = p.Status,
            SubmittedAt = p.SubmittedAt ?? DateTime.UtcNow
        }).ToList();
    }
    #endregion

    #region Get All Proposals (Admin)
    public async Task<IEnumerable<ProposalDto>> GetAllProposalsAsync(GetAllProposalsQuery request, CancellationToken cancellationToken = default)
    {
        var proposals = await _context.Set<Proposal>()
            .Include(p => p.JobPosts)
            .Include(p => p.FreelancerProfiles.User)
            .OrderByDescending(p => p.SubmittedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return proposals.Select(p => new ProposalDto
        {
            ProposalsId = p.ProposalsId,
            JobPostsId = p.JobPostsId,
            JobTitle = p.JobPosts?.Title ?? "",
            FreelancerProfilesId = p.FreelancerProfilesId,
            FreelancerName = p.FreelancerProfiles?.User?.FullName ?? "",
            CoverLetter = p.CoverLetter ?? "",
            ProposedRate = p.ProposedRate ?? 0m,
            ProposedDuration = p.ProposedDuration ?? "",
            Status = p.Status,
            SubmittedAt = p.SubmittedAt ?? DateTime.UtcNow,
        }).ToList();
    }
    #endregion
}