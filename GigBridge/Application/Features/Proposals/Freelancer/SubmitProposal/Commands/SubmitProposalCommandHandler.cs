using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.Commands;

public class SubmitProposalCommandHandler : IRequestHandler<SubmitProposalCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public SubmitProposalCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Guid> Handle(SubmitProposalCommand command, CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(job => job.JobPostsId == command.Request.JobPostsId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        EnsureJobPostAcceptsProposals(jobPost);
        await EnsureProposalHasNotBeenSubmittedAsync(command, freelancerProfile.FreelancerProfilesId, cancellationToken);

        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = command.Request.JobPostsId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            CoverLetter = command.Request.CoverLetter?.Trim(),
            ProposedRate = command.Request.ProposedRate,
            ProposedDuration = command.Request.ProposedDuration,
            Status = 0,
            SubmittedAt = _dateTimeService.UtcNow
        };

        _context.Set<Proposal>().Add(proposal);
        await _context.SaveChangesAsync(cancellationToken);

        return proposal.ProposalsId;
    }

    private void EnsureJobPostAcceptsProposals(JobPost jobPost)
    {
        if (jobPost.Status != 1)
        {
            throw new BadRequestException("This job post is not accepting proposals.");
        }

        if (jobPost.EndDate.HasValue && jobPost.EndDate.Value <= _dateTimeService.UtcNow)
        {
            throw new BadRequestException("This job post application deadline has passed.");
        }
    }

    private async Task EnsureProposalHasNotBeenSubmittedAsync(
        SubmitProposalCommand command,
        Guid freelancerProfileId,
        CancellationToken cancellationToken)
    {
        var hasSubmitted = await _context.Set<Proposal>()
            .AnyAsync(proposal =>
                proposal.JobPostsId == command.Request.JobPostsId &&
                proposal.FreelancerProfilesId == freelancerProfileId,
                cancellationToken);

        if (hasSubmitted)
        {
            throw new BadRequestException("You have already submitted a proposal for this job.");
        }
    }
}
