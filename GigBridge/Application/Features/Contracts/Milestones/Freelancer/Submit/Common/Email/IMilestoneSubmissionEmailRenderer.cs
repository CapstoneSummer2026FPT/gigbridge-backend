namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Common.Email;

public interface IMilestoneSubmissionEmailRenderer
{
    RenderedMilestoneSubmissionEmail Render(MilestoneSubmissionEmailModel model);
}
