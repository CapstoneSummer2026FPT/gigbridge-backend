using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed class SubmitMilestoneCommandHandler :
    IRequestHandler<SubmitMilestoneCommand, ContractMilestoneResponse>
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

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

        ValidateRequest(command);

        var now = _dateTimeService.UtcNow;
        var existingAttachments = await _context.Set<MilestoneAttachment>()
            .Where(attachment => attachment.MilestonesId == milestone.MilestonesId)
            .ToListAsync(cancellationToken);

        if (existingAttachments.Count > 0)
        {
            _context.Set<MilestoneAttachment>().RemoveRange(existingAttachments);
            milestone.MilestoneAttachments.Clear();
        }

        var attachment = await CreateAttachmentAsync(command, milestone.MilestonesId, now, cancellationToken);
        _context.Set<MilestoneAttachment>().Add(attachment);
        milestone.MilestoneAttachments.Add(attachment);

        milestone.SubmissionDescription = NormalizeDescription(command.Description);
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

    private static void ValidateRequest(SubmitMilestoneCommand command)
    {
        var hasFile = command.File is not null;
        var hasExternalUrl = !string.IsNullOrWhiteSpace(command.ExternalUrl);

        if (hasFile == hasExternalUrl)
        {
            throw new BadRequestException("Provide exactly one milestone file or external URL.");
        }

        if (command.Description is not null && command.Description.Length > 5000)
        {
            throw new BadRequestException("Submission description exceeds 5000 characters.");
        }

        if (command.File is null)
        {
            if (!Uri.TryCreate(command.ExternalUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new BadRequestException("External URL must be a valid HTTP or HTTPS URL.");
            }

            return;
        }

        if (command.File.Length <= 0 || command.File.Length > MaxFileSizeBytes)
        {
            throw new BadRequestException("Milestone file size is invalid.");
        }

        if (string.IsNullOrWhiteSpace(command.File.FileName))
        {
            throw new BadRequestException("Milestone file name is required.");
        }
    }

    private async Task<MilestoneAttachment> CreateAttachmentAsync(
        SubmitMilestoneCommand command,
        Guid milestoneId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (command.File is null)
        {
            return new MilestoneAttachment
            {
                MilestoneAttachmentsId = Guid.NewGuid(),
                MilestonesId = milestoneId,
                FileName = "External URL",
                FileUrl = command.ExternalUrl!.Trim(),
                FileSize = null,
                SourceType = (int)MilestoneSubmissionSourceType.Link,
                MimeType = null,
                UploadedByUserId = command.UserId,
                CreatedAt = now
            };
        }

        if (_mediaService == null)
        {
            throw new InvalidOperationException("MediaService is not configured for file uploads.");
        }

        var fileUrl = await _mediaService.UploadFileAsync(
            command.File.Content,
            command.File.FileName,
            command.File.ContentType,
            "milestones",
            cancellationToken);

        return new MilestoneAttachment
        {
            MilestoneAttachmentsId = Guid.NewGuid(),
            MilestonesId = milestoneId,
            FileName = command.File.FileName.Trim(),
            FileUrl = fileUrl,
            FileSize = command.File.Length,
            SourceType = (int)MilestoneSubmissionSourceType.File,
            MimeType = string.IsNullOrWhiteSpace(command.File.ContentType)
                ? null
                : command.File.ContentType.Trim(),
            UploadedByUserId = command.UserId,
            CreatedAt = now
        };
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
