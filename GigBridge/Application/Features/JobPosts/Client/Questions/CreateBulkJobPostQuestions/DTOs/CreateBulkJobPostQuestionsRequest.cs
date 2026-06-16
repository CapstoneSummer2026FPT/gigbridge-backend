namespace Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.DTOs;

public class CreateBulkJobPostQuestionsRequest
{
    public List<CreateBulkJobPostQuestionItemRequest>? Questions { get; set; }
}

public class CreateBulkJobPostQuestionItemRequest
{
    public string? QuestionText { get; set; }

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;
}
