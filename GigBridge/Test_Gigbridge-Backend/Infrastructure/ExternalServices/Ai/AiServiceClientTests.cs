using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Infrastructure.ExternalServices.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Ai;

public class AiServiceClientTests
{
    private readonly IOptions<AiServiceOptions> _options;

    public AiServiceClientTests()
    {
        _options = Options.Create(new AiServiceOptions
        {
            BaseUrl = "http://localhost:8000",
            ApiKey = "test-key"
        });
    }

    [Fact]
    public async Task GenerateJobDescriptionAsync_ReturnsData_OnSuccess()
    {
        // Arrange
        var expectedResponse = new ApiResponse<JobPostGenerationResponseDto>
        {
            Success = true,
            StatusCode = 200,
            Message = "Success",
            Data = new JobPostGenerationResponseDto
            {
                Title = "Senior React Developer",
                MajorId = "major-1",
                CategoryId = "category-1",
                SystemSkillIds = new List<string> { "skill-1" },
                CustomSkills = new List<string> { "Tailwind" },
                Description = "Job Description content",
                QuestionRecruitment = new List<string> { "Question 1" }
            }
        };

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var httpClient = new HttpClient(handler);
        var client = new AiServiceClient(httpClient, _options);

        var request = new JobPostGenerationRequestDto
        {
            ClientPrompt = "Looking for a React developer",
            AllowedMajors = new List<MajorOptionDto>(),
            AllowedCategories = new List<CategoryOptionDto>(),
            AvailableSkills = new List<SkillOptionDto>()
        };

        // Act
        var result = await client.GenerateJobDescriptionAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Senior React Developer", result.Title);
        Assert.Equal("major-1", result.MajorId);
        Assert.Equal("category-1", result.CategoryId);
        Assert.Single(result.SystemSkillIds);
        Assert.Contains("Tailwind", result.CustomSkills);
    }

    [Fact]
    public async Task GenerateJobDescriptionAsync_ThrowsBadRequestException_OnPolicyViolation()
    {
        // Arrange
        var errorResponse = new ApiResponse<object>
        {
            Success = false,
            StatusCode = 400,
            Message = "The request violates platform safety guidelines against illegal or harmful activities.",
            Errors = new List<string> { "policy_violation" }
        };

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, errorResponse);
        var httpClient = new HttpClient(handler);
        var client = new AiServiceClient(httpClient, _options);

        var request = new JobPostGenerationRequestDto
        {
            ClientPrompt = "We need a skilled drug dealer...",
            AllowedMajors = new List<MajorOptionDto>(),
            AllowedCategories = new List<CategoryOptionDto>(),
            AvailableSkills = new List<SkillOptionDto>()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            client.GenerateJobDescriptionAsync(request, CancellationToken.None));

        Assert.Equal("The request violates platform safety guidelines against illegal or harmful activities.", exception.Message);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseContent;

        public MockHttpMessageHandler(HttpStatusCode statusCode, object responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_responseContent)
            };
            return await Task.FromResult(response);
        }
    }
}
