namespace Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;

public class UpdateStatusJobPostRequest
{
    /// <summary>
    /// Enum JobPostStatus: 0=Draft, 1=Open, 2=Closed, 3=Cancelled
    /// </summary>
    public int Status { get; set; }
}