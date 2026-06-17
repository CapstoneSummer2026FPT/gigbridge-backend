using Application.Features.Profiles.ClientProfile.UpdateClientProfile.Commands;
using Application.Features.Profiles.ClientProfile.UpdateClientProfile.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Client;

public class UpdateClientProfileCommandValidatorTests
{
    private readonly UpdateClientProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrorsForValidRequest()
    {
        var command = new UpdateClientProfileCommand(CreateValidDto());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorsForMissingRequiredFields()
    {
        var dto = CreateValidDto();
        dto.CompanyName = "";
        dto.Industry = "";
        dto.Location = "";

        var result = _validator.Validate(new UpdateClientProfileCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.CompanyName");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Industry");
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.Location");
    }

    [Fact]
    public void Validate_ReturnsErrorForInvalidCompanySize()
    {
        var dto = CreateValidDto();
        dto.CompanySize = 4;

        var result = _validator.Validate(new UpdateClientProfileCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Dto.CompanySize");
    }

    private static UpdateClientProfileDto CreateValidDto()
    {
        return new UpdateClientProfileDto
        {
            CompanyName = "Acme Labs",
            CompanyWebsite = "https://acme.example",
            CompanySize = 1,
            Industry = "Technology",
            CompanyDescription = "Building reliable SaaS tools.",
            Location = "Ho Chi Minh City"
        };
    }
}
