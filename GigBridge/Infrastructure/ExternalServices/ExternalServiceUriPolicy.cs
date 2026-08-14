namespace Infrastructure.ExternalServices;

internal static class ExternalServiceUriPolicy
{
    internal static bool IsLocalEnvironment(string? environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Local", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    internal static bool IsAllowed(string? value, bool allowLocalHttp)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            || (allowLocalHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }
}
