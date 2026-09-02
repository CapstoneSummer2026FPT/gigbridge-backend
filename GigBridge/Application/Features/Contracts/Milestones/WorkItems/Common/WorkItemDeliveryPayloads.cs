namespace Application.Features.Contracts.Milestones.WorkItems.Common;

/// <summary>Outbox payload for the "freelancer submitted work items" email to the client.</summary>
public sealed record WorkItemSubmissionDeliveryPayload(
    Guid ContractId,
    Guid MilestoneId,
    string MilestoneTitle,
    IReadOnlyList<string> WorkItemTitles,
    string RecipientEmail,
    string RecipientName);

/// <summary>Outbox payload for the "client requested revision" email to the freelancer.</summary>
public sealed record WorkItemRevisionDeliveryPayload(
    Guid ContractId,
    Guid MilestoneId,
    string MilestoneTitle,
    IReadOnlyList<string> WorkItemTitles,
    string Reason,
    string RecipientEmail,
    string RecipientName);

/// <summary>Outbox payload for the "milestone completed" email to the freelancer.</summary>
public sealed record MilestoneAutoCompletedDeliveryPayload(
    Guid ContractId,
    Guid MilestoneId,
    string MilestoneTitle,
    Guid? NextMilestoneId,
    string? NextMilestoneTitle,
    string RecipientEmail,
    string RecipientName);
