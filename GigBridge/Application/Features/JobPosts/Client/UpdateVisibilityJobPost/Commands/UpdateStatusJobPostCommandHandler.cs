using System;
using System.Linq;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;

public class UpdateStatusJobPostCommandHandler
    : IRequestHandler<UpdateStatusJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateStatusJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(
        UpdateStatusJobPostCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(
                profile => profile.UserId == command.UserId,
                cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .FirstOrDefaultAsync(
                jobPost =>
                    jobPost.JobPostsId == command.JobPostId &&
                    jobPost.ClientProfilesId == clientProfile.ClientProfilesId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to update it.");
        }

        if (command.Request.Status == 1) // Open status
        {
            var contract = await _context.Set<Contract>()
                .Include(c => c.Milestones)
                .FirstOrDefaultAsync(c => c.JobPostsId == jobPost.JobPostsId, cancellationToken);

            if (contract is null)
            {
                throw new BadRequestException("Draft contract must exist before publishing the job post.");
            }

            // Verify JobPost E-sign document is FullySigned
            var isEsignFullySigned = await _context.Set<EsignDocument>()
                .AnyAsync(
                    doc => doc.JobPostsId == jobPost.JobPostsId &&
                           doc.Status == (int)ESignDocumentStatus.FullySigned,
                    cancellationToken);

            if (!isEsignFullySigned)
            {
                throw new BadRequestException("Job post e-sign document is not fully signed.");
            }

            // Validate Milestones
            if (contract.Milestones.Count == 0)
            {
                throw new BadRequestException("At least one milestone is required.");
            }

            foreach (var milestone in contract.Milestones)
            {
                if (string.IsNullOrWhiteSpace(milestone.Title))
                {
                    throw new BadRequestException("Milestone title cannot be empty.");
                }

                if (milestone.Amount <= 0)
                {
                    throw new BadRequestException("Milestone amount must be positive.");
                }
            }

        }

        jobPost.Status = command.Request.Status;
        jobPost.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
