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

    private static CreateFreelancerProfileDto CreateValidDto()
    {
        return new CreateFreelancerProfileDto
        {
            Title = "Backend Developer",
            Bio = "Experienced .NET developer focused on clean application architecture.",
            Availability = 0,
            Location = "Ho Chi Minh City"
        };
    }
}
