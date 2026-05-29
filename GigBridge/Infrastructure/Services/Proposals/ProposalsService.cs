using Application.Common.Interfaces.IRepository;
using Application.Features.Proposals.Services;
using Application.Features.Proposals.SubmitProposal.Commands;
using Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.Proposals;

public class ProposalsService : IProposalsService
{
    private readonly IProposalRepository _proposalRepository;
    private readonly IJobPostRepository _jobPostRepository;

    public ProposalsService(
        IProposalRepository proposalRepository,
        IJobPostRepository jobPostRepository)
    {
        _proposalRepository = proposalRepository;
        _jobPostRepository = jobPostRepository;
    }

    public async Task<Guid> SubmitProposalAsync(SubmitProposalCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        // Kiểm tra JobPost tồn tại
        var jobPost = await _jobPostRepository.GetAsync(j => j.JobPostsId == request.JobPostsId);
        if (jobPost == null)
            throw new Exception("Job Post không tồn tại.");

        // Kiểm tra JobPost còn mở không
        if (jobPost.Status != 1)
            throw new Exception("Job Post này hiện không nhận thêm Proposal.");

        // Kiểm tra Freelancer đã submit chưa
        bool hasSubmitted = await _proposalRepository.HasUserSubmittedProposalAsync(
            request.JobPostsId,
            command.FreelancerProfilesId);

        if (hasSubmitted)
            throw new Exception("Bạn đã gửi Proposal cho công việc này rồi.");

        // Tạo Proposal
        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = request.JobPostsId,
            FreelancerProfilesId = command.FreelancerProfilesId,
            CoverLetter = request.CoverLetter,
            ProposedRate = request.ProposedRate,
            ProposedDuration = request.ProposedDuration,
            Status = 0, // 0 = Pending
            SubmittedAt = DateTime.UtcNow
        };

        _proposalRepository.Add(proposal);
        // await _unitOfWork.SaveChangesAsync(cancellationToken);

        return proposal.ProposalsId;
    }
}