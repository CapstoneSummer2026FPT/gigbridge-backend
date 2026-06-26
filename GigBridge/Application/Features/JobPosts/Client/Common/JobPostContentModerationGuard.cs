using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;

namespace Application.Features.JobPosts.Client.Common;

internal static class JobPostContentModerationGuard
{
    public static void EnsureAllowed(
        IContentModerationService contentModerationService,
        string? title,
        string? description)
    {
        var moderationResult = contentModerationService.ValidateJobPostContent(title, description);

        if (moderationResult.IsAllowed)
        {
            return;
        }

        throw new ValidationException(new Dictionary<string, string[]>
        {
            ["JobPostContent"] = new[] { ContentModerationMessages.JobPostContentViolation }
        });
    }
}
