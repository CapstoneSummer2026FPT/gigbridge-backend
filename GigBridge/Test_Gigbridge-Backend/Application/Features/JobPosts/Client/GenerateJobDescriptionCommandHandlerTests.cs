using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.JobPosts.Client.GenerateJobDescription.Commands;
using Application.Features.JobPosts.Client.GenerateJobDescription.DTOs;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;
using Xunit;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class GenerateJobDescriptionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ResolvesMajorAndCategoryNamesForDisplay()
    {
        // Arrange
        var context = new InMemoryApplicationDbContext();
        var majorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var majorCategoryId = Guid.NewGuid();

        var major = new Major
        {
            MajorsId = majorId,
            Name = "Technology & Design",
            Slug = "technology-design",
            IsActive = true
        };

        var category = new Category
        {
            CategoriesId = categoryId,
            Name = "Frontend Engineering",
            Slug = "frontend-engineering",
            IsActive = true
        };

        var majorCategory = new MajorCategory
        {
            MajorCategoriesId = majorCategoryId,
            MajorId = majorId,
            CategoryId = categoryId,
            Major = major,
            Category = category
        };

        context.AddSet(major);
        context.AddSet(category);
        context.AddSet(majorCategory);

        var aiResponse = new JobPostGenerationResponseDto
        {
            Title = "Senior React Developer",
            MajorId = majorId.ToString(),
            CategoryId = categoryId.ToString(),
            SystemSkillIds = new List<string>(),
            CustomSkills = new List<string>(),
            Description = "We are looking for a Senior React Developer...",
            QuestionRecruitment = new List<string> { "What is React?", "Explain TypeScript generic types." }
        };

        var fakeAiClient = new FakeAiServiceClient { ResponseToReturn = aiResponse };
        var fakeDateTime = new FakeDateTimeService(DateTime.UtcNow);

        var handler = new GenerateJobDescriptionCommandHandler(context, fakeAiClient, fakeDateTime);
        var command = new GenerateJobDescriptionCommand("React, TypeScript developer");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Senior React Developer", result.Title);
        Assert.Equal(majorId, result.MajorId);
        Assert.Equal("Technology & Design", result.MajorName);
        Assert.Equal(categoryId, result.CategoryId);
        Assert.Equal("Frontend Engineering", result.CategoryName);
        Assert.Equal(majorCategoryId, result.MajorCategoryId);
        Assert.Equal("We are looking for a Senior React Developer...", result.Description);
        Assert.NotNull(result.QuestionRecruitment);
        Assert.Equal(2, result.QuestionRecruitment.Count);
        Assert.Contains("What is React?", result.QuestionRecruitment);
    }

    private class FakeAiServiceClient : IAiServiceClient
    {
        public JobPostGenerationResponseDto ResponseToReturn { get; set; } = null!;

        public Task<JobPostGenerationResponseDto> GenerateJobDescriptionAsync(
            JobPostGenerationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResponseToReturn);
        }
    }

    private class FakeDateTimeService : IDateTimeService
    {
        public FakeDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
