namespace Application.Features.JobPosts.Client.UpdateVisibilityJobPost.DTOs;

public class UpdateVisibilityJobPostRequest
{
    /// <summary>
    /// Enum JobPostVisibility: 0=Public, 2=InviteOnly
    /// </summary>
    public int Visibility { get; set; }
}