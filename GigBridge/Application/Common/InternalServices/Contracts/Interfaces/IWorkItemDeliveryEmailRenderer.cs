using Application.Common.InternalServices.Contracts.Models;

namespace Application.Common.InternalServices.Contracts.Interfaces;

/// <summary>
/// Renders the three emails of the work item delivery flow. One renderer rather than three because
/// all three are the same shape — headline, a list of work items, one call to action — and only the
/// copy differs; three near-identical HTML templates would drift apart the first time one is edited.
/// </summary>
public interface IWorkItemDeliveryEmailRenderer
{
    RenderedDeliveryEmail RenderSubmission(WorkItemSubmissionEmailModel model);

    RenderedDeliveryEmail RenderRevisionRequested(WorkItemRevisionEmailModel model);

    RenderedDeliveryEmail RenderMilestoneCompleted(MilestoneAutoCompletedEmailModel model);
}
