using System.Text.RegularExpressions;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Common;
using Application.Features.Auth.ForgotPassword.Commands;
using Application.Features.Auth.ForgotPassword.DTOs;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Auth.ForgotPassword;

public class SendEmailPasswordChangingCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEmailDoesNotExist_ReturnsWithoutObservableSideEffects()
    {
        // Arrange
        var context = new InMemoryApplicationDbContext();
        context.AddSet<User>();
        var cache = Substitute.For<ICacheService>();
        var emailSender = Substitute.For<IAuthEmailSender>();
        var handler = new SendEmailPasswordChangingCommandHandler(context, cache, emailSender);
        var command = new SendEmailPasswordChangingCommand(new ForgotPasswordRequest
        {
            Email = "missing@example.com"
        });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Empty(emailSender.ReceivedCalls());
        await cache.DidNotReceive().SetAsync(
            OtpSecurity.ChallengeKey(OtpPurpose.PasswordReset, "missing@example.com"),
            Arg.Any<OtpChallengeState>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailExists_StoresAndSendsSameSixDigitOtp()
    {
        // Arrange
        const string normalizedEmail = "known.user@example.com";
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Known User",
            Email = normalizedEmail
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var cache = Substitute.For<ICacheService>();
        var emailSender = Substitute.For<IAuthEmailSender>();
        var handler = new SendEmailPasswordChangingCommandHandler(context, cache, emailSender);
        var command = new SendEmailPasswordChangingCommand(new ForgotPasswordRequest
        {
            Email = "  KNOWN.USER@EXAMPLE.COM  "
        });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var challengeKey = OtpSecurity.ChallengeKey(
            OtpPurpose.PasswordReset,
            normalizedEmail);
        await cache.Received(1).RemoveAsync(challengeKey, CancellationToken.None);

        var setCall = Assert.Single(
            cache.ReceivedCalls(),
            call => call.GetMethodInfo().Name == nameof(ICacheService.SetAsync)
                && Equals(call.GetArguments()[0], challengeKey));
        var challenge = Assert.IsType<OtpChallengeState>(setCall.GetArguments()[1]);
        var otp = challenge.Otp;

        Assert.Equal(challengeKey, setCall.GetArguments()[0]);
        Assert.Matches(new Regex(@"^\d{6}$"), otp);
        Assert.Equal(TimeSpan.FromMinutes(5), setCall.GetArguments()[2]);
        Assert.Equal(CancellationToken.None, setCall.GetArguments()[3]);

        await emailSender.Received(1)
            .SendForgotPasswordOtpEmailAsync(user.Email, otp, CancellationToken.None);
    }
}
