namespace Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.DTOs;

public class CreateJobPostQuestionRequest
{
    public string? QuestionText { get; set; }

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;
}
