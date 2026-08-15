using Application.Common.InternalServices.JobInvitations.Models;

namespace Application.Common.InternalServices.JobInvitations.Interfaces;
public interface IJobInvitationEmailRenderer
{
    RenderedJobInvitationEmail Render(NewJobInvitationTemplate model);
}
