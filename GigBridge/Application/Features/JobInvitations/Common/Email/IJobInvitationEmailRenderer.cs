namespace Application.Features.JobInvitations.Common.Email;

public interface IJobInvitationEmailRenderer
{
    RenderedJobInvitationEmail Render(NewJobInvitationTemplate model);
}
