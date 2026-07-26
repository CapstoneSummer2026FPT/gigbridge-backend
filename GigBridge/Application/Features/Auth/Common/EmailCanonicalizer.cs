namespace Application.Features.Auth.Common;

internal static class EmailCanonicalizer
{
    public static string Canonicalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
