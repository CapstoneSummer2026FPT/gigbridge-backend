namespace Application.Features.Proposals.Freelancer.QuestionTimers.DTOs;

public class CompleteQuestionTimerRequest
{
    public string? AnswerText { get; set; }

    public int LockedReason { get; set; }
}
