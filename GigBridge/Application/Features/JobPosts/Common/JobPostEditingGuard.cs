using Application.Common.Exceptions;
using Domain.Entities;

namespace Application.Features.JobPosts.Common;

public static class JobPostEditingGuard
{
    public const int DraftStatus = 0;
    public const int PublicVisibility = 0;
    public const int InviteOnlyVisibility = 2;
    public const int AdminLockedVisibility = 3;

    public const string AdminLockedMessage =
        "This job post has been locked by an admin and cannot be updated.";

    public const string ContentLockedMessage =
        "Published public and invite-only job posts cannot be edited.";

    public const string PublicToInviteOnlyTransitionLockedMessage =
        "A published public job post cannot be changed to invite-only.";

    public const string DraftTransitionLockedMessage =
        "A published job post cannot be changed back to draft.";

    public static bool CanEditContent(int status, int? visibility)
    {
        if (visibility == AdminLockedVisibility)
        {
            return false;
        }

        return status == DraftStatus;
    }

    public static void EnsureContentCanBeEdited(JobPost jobPost)
    {
        EnsureNotAdminLocked(jobPost.Visibility);

        if (!CanEditContent(jobPost.Status, jobPost.Visibility))
        {
            throw new BadRequestException(ContentLockedMessage);
        }
    }

    public static void EnsureVisibilityTransitionAllowed(JobPost jobPost, int requestedVisibility)
    {
        EnsureNotAdminLocked(jobPost.Visibility);

        if (jobPost.Status == DraftStatus)
        {
            return;
        }

        var currentVisibility = jobPost.Visibility ?? PublicVisibility;
        if (currentVisibility == PublicVisibility &&
            requestedVisibility == InviteOnlyVisibility)
        {
            throw new BadRequestException(PublicToInviteOnlyTransitionLockedMessage);
        }
    }

    public static void EnsureStatusTransitionAllowed(JobPost jobPost, int requestedStatus)
    {
        EnsureNotAdminLocked(jobPost.Visibility);

        if (jobPost.Status != DraftStatus && requestedStatus == DraftStatus)
        {
            throw new BadRequestException(DraftTransitionLockedMessage);
        }
    }

    private static void EnsureNotAdminLocked(int? visibility)
    {
        if (visibility == AdminLockedVisibility)
        {
            throw new BadRequestException(AdminLockedMessage);
        }
    }
}
