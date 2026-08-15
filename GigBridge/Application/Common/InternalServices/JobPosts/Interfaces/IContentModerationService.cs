using Application.Common.InternalServices.JobPosts.Models;

namespace Application.Common.InternalServices.JobPosts.Interfaces;
public interface IContentModerationService
{
    ContentModerationResult ValidateJobPostContent(string? title, string? description);
}
