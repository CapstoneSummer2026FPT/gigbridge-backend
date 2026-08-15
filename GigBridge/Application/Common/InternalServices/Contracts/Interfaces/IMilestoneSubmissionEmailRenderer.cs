using Application.Common.InternalServices.Contracts.Models;

namespace Application.Common.InternalServices.Contracts.Interfaces;
public interface IMilestoneSubmissionEmailRenderer
{
    RenderedMilestoneSubmissionEmail Render(MilestoneSubmissionEmailModel model);
}
