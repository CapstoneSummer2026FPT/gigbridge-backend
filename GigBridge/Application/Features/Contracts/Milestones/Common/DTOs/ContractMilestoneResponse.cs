using System;
using System.Collections.Generic;

namespace Application.Features.Contracts.Milestones.Common.DTOs;

public sealed record MilestoneAttachmentResponse(
    Guid MilestoneAttachmentsId,
    Guid MilestonesId,
    string FileName,
    string FileUrl,
    long? FileSize,
    int SourceType,
    string? MimeType,
    Guid? UploadedByUserId,
    DateTime CreatedAt);

public sealed record ContractWorkItemResponse(
    Guid WorkItemId,
    Guid MilestoneId,
    string Title,
    string? Description,
    string? Deliverables,
    string? EstimatedDuration,
    int OrderIndex,
    int Status,
    string? ProgressNote,
    DateTime? CompletedAt,
    DateTime? UpdatedAt);

public sealed record ContractMilestoneResponse(
    Guid MilestoneId,
    Guid ContractId,
    string Title,
    string? Description,
    decimal Amount,
    string? EstimatedDuration,
    DateOnly? DueDate,
    string? Deliverables,
    string? AcceptanceCriteria,
    int Status,
    int? SortOrder,
    DateTime? StartedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    decimal ReleasedAmount,
    DateTime? LastReleasedAt,
    string? SubmissionDescription,
    IReadOnlyList<MilestoneAttachmentResponse> Attachments,
    IReadOnlyList<ContractWorkItemResponse> WorkItems);
