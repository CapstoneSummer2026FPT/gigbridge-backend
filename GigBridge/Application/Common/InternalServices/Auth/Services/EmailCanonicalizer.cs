namespace Application.Common.InternalServices.Auth.Services;
internal static class EmailCanonicalizer
{
    public static string Canonicalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
