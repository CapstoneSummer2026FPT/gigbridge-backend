using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed class SubmitMilestoneCommandHandler :
    IRequestHandler<SubmitMilestoneCommand, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService? _mediaService;

    public SubmitMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService? mediaService = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
    }

    public async Task<ContractMilestoneResponse> Handle(
        SubmitMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.InProgress)
        {
            throw new BadRequestException("Only in-progress milestones can be submitted.");
        }

        var now = _dateTimeService.UtcNow;

        if (command.Files != null && command.Files.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(command.Description))
            {
                throw new BadRequestException("Submission description cannot be empty.");
            }

            if (command.Description.Length > 5000)
            {
                throw new BadRequestException("Submission description exceeds 5000 characters.");
            }

            if (_mediaService == null)
            {
                throw new InvalidOperationException("MediaService is not configured for file uploads.");
            }

            foreach (var file in command.Files)
            {
                if (file.Length > 100 * 1024 * 1024)
                {
                    throw new BadRequestException($"File {file.FileName} exceeds the 100MB size limit.");
                }

                var fileUrl = await _mediaService.UploadFileAsync(
                    file.Content,
                    file.FileName,
                    file.ContentType,
                    "milestones",
                    cancellationToken);

                var attachment = new MilestoneAttachment
                {
                    MilestoneAttachmentsId = Guid.NewGuid(),
                    MilestonesId = milestone.MilestonesId,
                    FileName = file.FileName,
                    FileUrl = fileUrl,
                    FileSize = file.Length,
                    UploadedByUserId = command.UserId,
                    CreatedAt = now
                };

                _context.Set<MilestoneAttachment>().Add(attachment);
            }

            milestone.SubmissionDescription = command.Description;
        }

        milestone.Status = (int)MilestoneStatus.Submitted;
        milestone.SubmittedAt = now;
        milestone.UpdatedAt = now;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone submitted: {milestone.Title}.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MilestoneWorkflowGuard.ToResponse(milestone);
    }
}
