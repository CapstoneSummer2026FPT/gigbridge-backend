namespace Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestion.DTOs;

public class UpdateJobPostQuestionRequest
{
    public string? QuestionText { get; set; }

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;
}
