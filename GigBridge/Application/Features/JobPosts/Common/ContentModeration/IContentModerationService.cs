namespace Application.Features.JobPosts.Common.ContentModeration;

public interface IContentModerationService
{
    ContentModerationResult ValidateJobPostContent(string? title, string? description);
}
