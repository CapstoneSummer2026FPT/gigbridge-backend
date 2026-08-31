using Microsoft.Extensions.Options;

namespace Application.Common.Options;

public sealed class AuthSessionOptionsValidator : IValidateOptions<AuthSessionOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthSessionOptions options)
    {
        if (options.MaxActiveSessionsPerUser is < 1 or > 20)
        {
            return ValidateOptionsResult.Fail(
                "AuthSessions:MaxActiveSessionsPerUser must be between 1 and 20.");
        }

        return ValidateOptionsResult.Success;
    }
}
