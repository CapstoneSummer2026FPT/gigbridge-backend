namespace Application.Features.JobInvitations.Common.Email;

public sealed record NewJobInvitationTemplate(
    string FreelancerName,
    string JobTitle,
    string ClientName,
    string Budget,
    string Deadline,
    string ShortDescription,
    string ActionUrl);
