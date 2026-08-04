namespace Application.Features.Proposals.Common.Email;

public interface IProposalNegotiationEmailRenderer
{
    RenderedProposalNegotiationEmail Render(ProposalNegotiationEmailModel model);
}
