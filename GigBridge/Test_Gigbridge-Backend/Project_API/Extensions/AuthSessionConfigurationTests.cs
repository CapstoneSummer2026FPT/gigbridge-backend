using Application.Common.Options;

namespace Test_Gigbridge_Backend.Project_API.Extensions;

public sealed class AuthSessionConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void OptionsValidatorRejectsUnsafeSessionLimit(int maximumSessions)
    {
        var result = new AuthSessionOptionsValidator().Validate(
            null,
            new AuthSessionOptions { MaxActiveSessionsPerUser = maximumSessions });

        Assert.True(result.Failed);
    }
}
