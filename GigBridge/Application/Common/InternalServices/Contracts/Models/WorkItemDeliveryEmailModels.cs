namespace Application.Common.InternalServices.Contracts.Models;

public sealed record RenderedDeliveryEmail(string Subject, string HtmlBody, string TextBody);

/// <summary>Freelancer submitted one or more work items — sent to the client.</summary>
public sealed record WorkItemSubmissionEmailModel(
    string RecipientName,
    string MilestoneTitle,
    IReadOnlyList<string> WorkItemTitles,
    string ActionUrl);

/// <summary>Client sent work items back — sent to the freelancer.</summary>
public sealed record WorkItemRevisionEmailModel(
    string RecipientName,
    string MilestoneTitle,
    IReadOnlyList<string> WorkItemTitles,
    string Reason,
    string ActionUrl);

/// <summary>Every work item approved, so the milestone closed — sent to the freelancer.</summary>
public sealed record MilestoneAutoCompletedEmailModel(
    string RecipientName,
    string MilestoneTitle,
    string? NextMilestoneTitle,
    string ActionUrl);
