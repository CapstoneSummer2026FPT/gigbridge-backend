using Application.Common.Exceptions;
using Application.Common.InternalServices.JobPosts.Interfaces;
using Application.Common.InternalServices.JobPosts.Models;

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
            ["JobPostContent"] = GetViolationMessages(moderationResult).ToArray()
        });
    }

    private static IEnumerable<string> GetViolationMessages(ContentModerationResult moderationResult)
    {
        var violations = moderationResult.Violations
            .Where(violation => !string.IsNullOrWhiteSpace(violation))
            .Distinct()
            .ToArray();

        return violations.Length > 0
            ? violations
            : new[] { ContentModerationMessages.JobPostContentViolation };
    }
}
