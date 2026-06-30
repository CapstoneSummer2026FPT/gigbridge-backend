namespace Application.Features.JobInvitations.Common.Email;

public sealed record RenderedJobInvitationEmail(string Subject, string HtmlBody, string TextBody);
