using Application.Common.InternalServices.Proposals.Models;

namespace Application.Common.InternalServices.Proposals.Interfaces;
public interface IProposalNegotiationEmailRenderer
{
    RenderedProposalNegotiationEmail Render(ProposalNegotiationEmailModel model);
}
