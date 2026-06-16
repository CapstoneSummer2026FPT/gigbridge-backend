using Application.Common.Models;
using Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.Commands;
using Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.DTOs;
using Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.Commands;
using Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.DTOs;
using Application.Features.JobPosts.Client.Questions.DeleteJobPostQuestion.Commands;
using Application.Features.JobPosts.Client.Questions.UpdateBulkJobPostQuestions.Commands;
using Application.Features.JobPosts.Client.Questions.UpdateBulkJobPostQuestions.DTOs;
using Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestion.Commands;
using Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestion.DTOs;
using Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestionRequired.Commands;
using Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestionRequired.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.Common.Questions.GetJobPostQuestions.Queries;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Common.JobPosts;

[ApiController]
[Route("api/JobPosts/{jobPostId:guid}/questions")]
[Authorize]
public class JobPostQuestionsController : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = "Client,Freelancer")]
    public async Task<IActionResult> GetQuestions(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetJobPostQuestionsQuery(jobPostId, userId, GetCurrentRole());
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<IEnumerable<JobPostQuestionDto>>.Ok(result, "Success"));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> CreateQuestion(
        Guid jobPostId,
        [FromBody] CreateJobPostQuestionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new CreateJobPostQuestionCommand(jobPostId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<JobPostQuestionDto>.Ok(result, "Question created successfully"));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> CreateQuestionsBulk(
        Guid jobPostId,
        [FromBody] CreateBulkJobPostQuestionsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new CreateBulkJobPostQuestionsCommand(jobPostId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<IEnumerable<JobPostQuestionDto>>.Ok(result, "Questions created successfully"));
    }

    [HttpPatch("{questionId:guid}")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> UpdateQuestion(
        Guid jobPostId,
        Guid questionId,
        [FromBody] UpdateJobPostQuestionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateJobPostQuestionCommand(jobPostId, questionId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<JobPostQuestionDto>.Ok(result, "Question updated successfully"));
    }

    [HttpPatch("{questionId:guid}/required")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> UpdateQuestionRequired(
        Guid jobPostId,
        Guid questionId,
        [FromBody] UpdateJobPostQuestionRequiredRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateJobPostQuestionRequiredCommand(jobPostId, questionId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<JobPostQuestionDto>.Ok(result, "Question required flag updated successfully"));
    }

    [HttpPatch("bulk")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> UpdateQuestionsBulk(
        Guid jobPostId,
        [FromBody] UpdateBulkJobPostQuestionsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateBulkJobPostQuestionsCommand(jobPostId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<IEnumerable<JobPostQuestionDto>>.Ok(result, "Questions updated successfully"));
    }

    [HttpDelete("{questionId:guid}")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> DeleteQuestion(Guid jobPostId, Guid questionId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new DeleteJobPostQuestionCommand(jobPostId, questionId, userId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(result, "Question deleted successfully"));
    }

    private string GetCurrentRole()
    {
        if (User.IsInRole(nameof(UserRole.Client)))
        {
            return nameof(UserRole.Client);
        }

        if (User.IsInRole(nameof(UserRole.Freelancer)))
        {
            return nameof(UserRole.Freelancer);
        }

        return string.Empty;
    }
}
