using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public class CreateFreelancerProfileCommandValidatorTests
{
    private readonly CreateFreelancerProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new CreateFreelancerProfileCommand(CreateValidDto());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenHourlyRateIsZero()
    {
        var dto = CreateValidDto();
        dto.HourlyRate = 0;
        var command = new CreateFreelancerProfileCommand(dto);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.HourlyRate");
    }

    [Fact]
    public void Validate_ReturnsErrorWhenExperienceLevelIsOutsideSupportedRange()
    {
        var dto = CreateValidDto();
        dto.ExperienceLevel = 9;
        var command = new CreateFreelancerProfileCommand(dto);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.ExperienceLevel");
    }

    private static CreateFreelancerProfileDto CreateValidDto()
    {
        return new CreateFreelancerProfileDto
        {
            Title = "Backend Developer",
            Bio = "Experienced .NET developer focused on clean application architecture.",
            HourlyRate = 25m,
            ExperienceLevel = 1,
            Availability = 0,
            Location = "Ho Chi Minh City"
        };
    }
}
