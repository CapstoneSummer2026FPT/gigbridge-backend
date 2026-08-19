namespace Application.Common.InternalServices.Proposals.Models;
public class CompleteQuestionTimerRequest
{
    public string? AnswerText { get; set; }

    public int LockedReason { get; set; }
}
