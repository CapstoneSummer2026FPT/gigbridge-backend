using System;
using System.Collections.Generic;

namespace Application.Features.Contracts.Milestones.Common.DTOs;

public sealed record MilestoneAttachmentResponse(
    Guid MilestoneAttachmentsId,
    Guid MilestonesId,
    string FileName,
    string FileUrl,
    long? FileSize,
    Guid? UploadedByUserId,
    DateTime CreatedAt);

public sealed record ContractMilestoneResponse(
    Guid MilestoneId,
    Guid ContractId,
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int Status,
    int? SortOrder,
    DateTime? StartedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    decimal ReleasedAmount,
    DateTime? LastReleasedAt,
    string? SubmissionDescription,
    IReadOnlyList<MilestoneAttachmentResponse> Attachments);
