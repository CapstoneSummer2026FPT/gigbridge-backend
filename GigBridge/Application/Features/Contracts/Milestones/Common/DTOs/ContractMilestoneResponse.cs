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

/// <summary>
/// One attempt by the freelancer to deliver a work item, with the client's verdict. Ordered by
/// <paramref name="RevisionNumber"/>; earlier attempts stay visible after a resubmission so both
/// parties (and an admin resolving a dispute) can see what was delivered and why it was rejected.
/// </summary>
public sealed record ContractWorkItemSubmissionResponse(
    Guid SubmissionId,
    Guid WorkItemId,
    int RevisionNumber,
    string? Note,
    DateTime SubmittedAt,
    Guid SubmittedByUserId,
    int ReviewStatus,
    DateTime? ReviewedAt,
    Guid? ReviewedByUserId,
    string? ReviewReason,
    IReadOnlyList<MilestoneAttachmentResponse> Attachments);

public sealed record ContractWorkItemResponse(
    Guid WorkItemId,
    Guid MilestoneId,
    string Title,
    string? Description,
    string? Deliverables,
    string? EstimatedDuration,
    DateOnly? DueDate,
    int OrderIndex,
    int Status,
    string? ProgressNote,
    DateTime? CompletedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ContractWorkItemSubmissionResponse> Submissions);

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
    IReadOnlyList<ContractWorkItemResponse> WorkItems,
    /// <summary>Enum MilestoneDeliveryMode: 0=Legacy, 1=WorkItem. Drives which UI the client sees.</summary>
    int DeliveryMode);

/// <summary>
/// Returned by the client's bulk work item review so the acting browser can open the
/// "milestone complete" modal immediately, without waiting for the realtime round trip.
/// </summary>
public sealed record ReviewWorkItemsResponse(
    ContractMilestoneResponse Milestone,
    bool MilestoneCompleted,
    Guid? NextMilestoneId,
    string? NextMilestoneTitle);
