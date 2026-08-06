using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public class UpdateFreelancerProfileCommandValidatorTests
{
    private readonly UpdateFreelancerProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new UpdateFreelancerProfileCommand(CreateValidDto());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorsForMissingRequiredFields()
    {
        var dto = CreateValidDto();
        dto.Title = "";
        dto.Bio = "";
        dto.Location = "";

        var result = _validator.Validate(new UpdateFreelancerProfileCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Title");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Bio");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Location");
    }

    [Fact]
    public void Validate_ReturnsErrorForInvalidAvailability()
    {
        var dto = CreateValidDto();
        dto.Availability = 3;

        var result = _validator.Validate(new UpdateFreelancerProfileCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Availability");
    }

    [Fact]
    public void Validate_ReturnsErrorsForMissingOrDuplicateTaxonomy()
    {
        var dto = CreateValidDto();
        var categoryId = Guid.NewGuid();
        dto.MajorId = Guid.Empty;
        dto.CategoryIds = new[] { categoryId, categoryId };

        var result = _validator.Validate(new UpdateFreelancerProfileCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.MajorId");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.CategoryIds");
    }

    [Fact]
    public void Validate_ReturnsErrorsForInvalidPortfolioContent()
    {
        var dto = CreateValidDto();
        dto.PortfolioItems = new[]
        {
            new UpdatePortfolioItemDto
            {
                Title = "",
                ProjectUrl = "javascript:alert('xss')"
            }
        };

        var result = _validator.Validate(new UpdateFreelancerProfileCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith(".Title"));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith(".ProjectUrl"));
    }

    private static UpdateFreelancerProfileDto CreateValidDto()
    {
        return new UpdateFreelancerProfileDto
        {
            Title = "Backend Developer",
            Bio = "Experienced .NET developer focused on clean application architecture.",
            Availability = 0,
            Location = "Ho Chi Minh City",
            MajorId = Guid.NewGuid(),
            CategoryIds = new[] { Guid.NewGuid() }
        };
    }
}
