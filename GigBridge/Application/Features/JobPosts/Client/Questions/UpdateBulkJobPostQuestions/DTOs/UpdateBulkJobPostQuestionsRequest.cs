namespace Application.Features.JobPosts.Client.Questions.UpdateBulkJobPostQuestions.DTOs;

public class UpdateBulkJobPostQuestionsRequest
{
    public List<UpdateBulkJobPostQuestionItemRequest>? Questions { get; set; }
}

public class UpdateBulkJobPostQuestionItemRequest
{
    public Guid JobPostQuestionsId { get; set; }

    public string? QuestionText { get; set; }

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;
}
