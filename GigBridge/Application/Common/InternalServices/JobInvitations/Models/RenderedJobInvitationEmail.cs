namespace Application.Common.InternalServices.JobInvitations.Models;
public sealed record RenderedJobInvitationEmail(string Subject, string HtmlBody, string TextBody);
