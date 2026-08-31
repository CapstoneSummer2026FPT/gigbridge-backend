using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.InternalServices.Auth.Services;
using Application.Common.Interfaces.Time;
using Application.Common.Options;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Test_Gigbridge_Backend.TestSupport;

internal static class AuthSessionTestFactory
{
    public static IAuthSessionService Create(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        bool enabled = true,
        int maximumActiveSessions = 5)
    {
        return new AuthSessionService(
            context,
            jwtService,
            dateTimeService,
            Options.Create(new AuthSessionOptions
            {
                Enabled = enabled,
                MaxActiveSessionsPerUser = maximumActiveSessions
            }),
            NullLogger<AuthSessionService>.Instance);
    }
}
