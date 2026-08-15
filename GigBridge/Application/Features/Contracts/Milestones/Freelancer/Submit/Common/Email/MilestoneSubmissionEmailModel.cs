namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Common.Email;

public sealed record MilestoneSubmissionFileModel(
    string FileName,
    string TypeLabel,
    string? SizeLabel,
    string IconGlyph);

public sealed record MilestoneSubmissionEmailModel(
    string ClientName,
    string JobTitle,
    string MilestoneTitle,
    int MilestoneNumber,
    int MilestoneCount,
    DateTime? StartDate,
    DateOnly? Deadline,
    DateTime SubmittedAt,
    string StatusLabel,
    string FreelancerName,
    IReadOnlyList<MilestoneSubmissionFileModel> Files,
    string ActionUrl);

public sealed record RenderedMilestoneSubmissionEmail(string Subject, string HtmlBody, string TextBody);
