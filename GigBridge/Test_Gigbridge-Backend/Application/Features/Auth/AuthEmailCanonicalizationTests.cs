using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Time;
using Application.Features.Auth.Common.Interfaces;
using Application.Features.Elo.Common.Interfaces;
using Application.Features.Auth.GoogleLogin.Commands;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Common;
using Application.Features.Auth.Login.Commands;
using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.Register.Commands;
using Application.Features.Auth.Register.DTOs;
using Application.Features.Auth.SendOtp.Commands;
using Application.Features.Auth.SendOtp.DTOs;
using Application.Features.Auth.VerifyOtp.Commands;
using Application.Features.Auth.VerifyOtp.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums.Accounts;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Auth;

public class AuthEmailCanonicalizationTests
{
    private const string CanonicalEmail = "mixed.user@example.com";
    private const string MixedEmail = "  MiXeD.User@Example.COM  ";
    private const string SignupVerificationTicket =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Register_StoresCanonicalEmailAndUsesCanonicalVerificationKey()
    {
        // Arrange
        var context = new InMemoryApplicationDbContext();
        var users = context.AddSet<User>();
        var cache = Substitute.For<ICacheService>();
        var verificationKey = OtpSecurity.VerifiedKey(
            OtpPurpose.Signup,
            CanonicalEmail,
            SignupVerificationTicket);
        cache.GetAndRemoveAsync<bool>(
                verificationKey,
                CancellationToken.None)
            .Returns(true);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.HashPassword("StrongPass1!").Returns("password-hash");
        var dateTime = Substitute.For<IDateTimeService>();
        dateTime.UtcNow.Returns(new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc));
        var userElo = Substitute.For<IUserEloService>();
        var handler = new RegisterCommandHandler(
            context,
            passwordHasher,
            dateTime,
            cache,
            userElo,
            Substitute.For<IMapper>());
        var command = new RegisterCommand(new RegisterRequest
        {
            Email = MixedEmail,
            FullName = "Mixed User",
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!",
            VerificationTicket = SignupVerificationTicket,
            role = UserRole.Client
        });
        var validation = await new RegisterCommandValidator().ValidateAsync(command);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(validation.IsValid);
        var user = Assert.Single(users.Entities);
        Assert.Equal(CanonicalEmail, user.Email);
        await cache.Received(1)
            .GetAndRemoveAsync<bool>(verificationKey, CancellationToken.None);
    }

    [Fact]
    public async Task Register_RejectsVerificationTicketIssuedToAnotherClient()
    {
        const string attackerTicket =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        var context = new InMemoryApplicationDbContext();
        context.AddSet<User>();
        var cache = Substitute.For<ICacheService>();
        cache.GetAndRemoveAsync<bool>(
                OtpSecurity.VerifiedKey(
                    OtpPurpose.Signup,
                    CanonicalEmail,
                    SignupVerificationTicket),
                CancellationToken.None)
            .Returns(true);
        var handler = new RegisterCommandHandler(
            context,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IDateTimeService>(),
            cache,
            Substitute.For<IUserEloService>(),
            Substitute.For<IMapper>());
        var command = new RegisterCommand(new RegisterRequest
        {
            Email = CanonicalEmail,
            FullName = "Mixed User",
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!",
            VerificationTicket = attackerTicket,
            role = UserRole.Client
        });

        var action = () => handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(action);
        await cache.Received(1).GetAndRemoveAsync<bool>(
            OtpSecurity.VerifiedKey(
                OtpPurpose.Signup,
                CanonicalEmail,
                attackerTicket),
            CancellationToken.None);
        Assert.Equal(0, context.SaveChangesCount);
    }

    [Fact]
    public async Task Register_RejectsMixedCaseDuplicateOfCanonicalStoredEmail()
    {
        // Arrange
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Existing User",
            Email = CanonicalEmail
        });
        var cache = Substitute.For<ICacheService>();
        var handler = new RegisterCommandHandler(
            context,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IDateTimeService>(),
            cache,
            Substitute.For<IUserEloService>(),
            Substitute.For<IMapper>());
        var command = new RegisterCommand(new RegisterRequest
        {
            Email = MixedEmail,
            FullName = "Duplicate User",
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!",
            VerificationTicket = SignupVerificationTicket,
            role = UserRole.Freelancer
        });

        // Act
        var action = () => handler.Handle(command, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Equal("Email already exists", exception.Message);
        Assert.Empty(cache.ReceivedCalls());
    }

    [Fact]
    public async Task Login_FindsCanonicalStoredEmailFromTrimmedMixedCaseInput()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Mixed User",
            Email = CanonicalEmail,
            Password = "password-hash",
            IsActive = true,
            IsEmailVerified = true
        };
        var context = new InMemoryApplicationDbContext();
        context.AddSet(user);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.VerifyPassword("StrongPass1!", "password-hash").Returns(true);
        var dateTime = Substitute.For<IDateTimeService>();
        dateTime.UtcNow.Returns(new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc));
        var jwt = Substitute.For<IJwtService>();
        jwt.GenerateRefreshToken().Returns("refresh-token");
        jwt.HashRefreshToken("refresh-token").Returns("refresh-token-hash");
        jwt.GetRefreshTokenExpiryMinutes().Returns(60);
        jwt.GenerateToken(user).Returns("access-token");
        var handler = new LoginWithRefreshCommandHandler(
            context,
            passwordHasher,
            jwt,
            dateTime,
            Substitute.For<IUserEloService>(),
            Substitute.For<IMapper>());
        var command = new LoginWithRefreshCommand(new LoginRequest
        {
            Email = MixedEmail,
            Password = "StrongPass1!"
        });
        var validation = await new LoginWithRefreshCommandValidator().ValidateAsync(command);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(validation.IsValid);
        Assert.Equal("access-token", result.LoginData.Token);
        Assert.Equal("refresh-token-hash", user.RefreshTokenHash);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task SendOtp_UsesCanonicalEmailForCacheAndDelivery()
    {
        // Arrange
        var cache = Substitute.For<ICacheService>();
        var emailSender = Substitute.For<IAuthEmailSender>();
        var handler = new SendOtpCommandHandler(
            cache,
            emailSender,
            Substitute.For<ICurrentUserService>());
        var command = new SendOtpCommand(new SendOtpRequest
        {
            Email = MixedEmail,
            Purpose = OtpPurposeNames.Signup
        });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var challengeKey = OtpSecurity.ChallengeKey(OtpPurpose.Signup, CanonicalEmail);
        await cache.Received(1).RemoveAsync(challengeKey, CancellationToken.None);
        var setCall = Assert.Single(
            cache.ReceivedCalls(),
            call => call.GetMethodInfo().Name == nameof(ICacheService.SetAsync)
                && Equals(call.GetArguments()[0], challengeKey));
        var challenge = Assert.IsType<OtpChallengeState>(setCall.GetArguments()[1]);
        var otp = challenge.Otp;
        Assert.Equal(challengeKey, setCall.GetArguments()[0]);
        Assert.Matches(@"^\d{6}$", otp);
        await emailSender.Received(1)
            .SendOtpEmailAsync(CanonicalEmail, otp, CancellationToken.None);
    }

    [Fact]
    public async Task VerifyOtp_UsesCanonicalEmailForBothCacheKeys()
    {
        // Arrange
        const string otp = "123456";
        var cache = Substitute.For<ICacheService>();
        var challengeKey = OtpSecurity.ChallengeKey(OtpPurpose.Signup, CanonicalEmail);
        var challenge = new OtpChallengeState(otp, 0, DateTime.UtcNow.AddMinutes(5));
        cache.GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None)
            .Returns(challenge);
        var handler = new VerifyOtpCommandHandler(
            cache,
            Substitute.For<ICurrentUserService>());
        var command = new VerifyOtpCommand(new VerifyOtpRequest
        {
            Email = MixedEmail,
            Otp = otp,
            Purpose = OtpPurposeNames.Signup
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await cache.Received(1)
            .GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None);
        Assert.NotNull(result.VerificationTicket);
        Assert.Matches("^[a-f0-9]{64}$", result.VerificationTicket);
        await cache.Received(1).SetAsync(
            OtpSecurity.VerifiedKey(
                OtpPurpose.Signup,
                CanonicalEmail,
                result.VerificationTicket),
            true,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
    }

    [Fact]
    public async Task IdentityVerification_IsBoundToAuthenticatedEmailAndExactIdentityCode()
    {
        const string otp = "123456";
        const string identityCode = "001234567890";
        var cache = Substitute.For<ICacheService>();
        var challengeKey = OtpSecurity.ChallengeKey(
            OtpPurpose.IdentityVerification,
            CanonicalEmail);
        cache.GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None)
            .Returns(new OtpChallengeState(otp, 0, DateTime.UtcNow.AddMinutes(5)));
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Email.Returns(CanonicalEmail);
        var handler = new VerifyOtpCommandHandler(cache, currentUser);

        var result = await handler.Handle(
            new VerifyOtpCommand(new VerifyOtpRequest
            {
                Email = MixedEmail,
                Otp = otp,
                Purpose = OtpPurposeNames.IdentityVerification,
                IdentityOrTaxCode = "001 234 567 890"
            }),
            CancellationToken.None);

        Assert.NotNull(result.VerificationTicket);
        await cache.Received(1).SetAsync(
            OtpSecurity.VerifiedKey(
                OtpPurpose.IdentityVerification,
                CanonicalEmail,
                result.VerificationTicket,
                identityCode),
            true,
            OtpSecurity.ChallengeLifetime,
            CancellationToken.None);
    }

    [Fact]
    public async Task SendOtp_IdentityVerificationUsesDedicatedEmailTemplate()
    {
        var cache = Substitute.For<ICacheService>();
        var emailSender = Substitute.For<IAuthEmailSender>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Email.Returns(CanonicalEmail);
        var handler = new SendOtpCommandHandler(cache, emailSender, currentUser);

        await handler.Handle(
            new SendOtpCommand(new SendOtpRequest
            {
                Email = MixedEmail,
                Purpose = OtpPurposeNames.IdentityVerification
            }),
            CancellationToken.None);

        var challengeKey = OtpSecurity.ChallengeKey(
            OtpPurpose.IdentityVerification,
            CanonicalEmail);
        var setCall = Assert.Single(
            cache.ReceivedCalls(),
            call => call.GetMethodInfo().Name == nameof(ICacheService.SetAsync)
                && Equals(call.GetArguments()[0], challengeKey));
        var challenge = Assert.IsType<OtpChallengeState>(setCall.GetArguments()[1]);

        await emailSender.Received(1).SendIdentityVerificationOtpEmailAsync(
            CanonicalEmail,
            challenge.Otp,
            CancellationToken.None);
        await emailSender.DidNotReceiveWithAnyArgs().SendOtpEmailAsync(
            default!,
            default!,
            default);
    }

    [Fact]
    public async Task IdentityVerification_RejectsAnotherAccountEmail()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Email.Returns("other@example.com");
        var handler = new SendOtpCommandHandler(
            Substitute.For<ICacheService>(),
            Substitute.For<IAuthEmailSender>(),
            currentUser);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new SendOtpCommand(new SendOtpRequest
            {
                Email = CanonicalEmail,
                Purpose = OtpPurposeNames.IdentityVerification
            }),
            CancellationToken.None));
    }

    [Fact]
    public async Task SendOtp_RejectsResendDuringPurposeScopedCooldown()
    {
        var cache = Substitute.For<ICacheService>();
        cache.GetAsync<bool>(
                OtpSecurity.CooldownKey(OtpPurpose.Signup, CanonicalEmail),
                CancellationToken.None)
            .Returns(true);
        var emailSender = Substitute.For<IAuthEmailSender>();
        var handler = new SendOtpCommandHandler(
            cache,
            emailSender,
            Substitute.For<ICurrentUserService>());

        var action = () => handler.Handle(
            new SendOtpCommand(new SendOtpRequest
            {
                Email = MixedEmail,
                Purpose = OtpPurposeNames.Signup
            }),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Contains("wait", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(emailSender.ReceivedCalls());
    }

    [Fact]
    public async Task VerifyOtp_FifthFailureConsumesChallengeAndCreatesLockout()
    {
        const string challengeOtp = "123456";
        var cache = Substitute.For<ICacheService>();
        var challengeKey = OtpSecurity.ChallengeKey(OtpPurpose.PasswordReset, CanonicalEmail);
        var lockoutKey = OtpSecurity.LockoutKey(OtpPurpose.PasswordReset, CanonicalEmail);
        cache.GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None)
            .Returns(new OtpChallengeState(
                challengeOtp,
                OtpSecurity.MaxFailedAttempts - 1,
                DateTime.UtcNow.AddMinutes(5)));
        var handler = new VerifyOtpCommandHandler(
            cache,
            Substitute.For<ICurrentUserService>());

        var action = () => handler.Handle(
            new VerifyOtpCommand(new VerifyOtpRequest
            {
                Email = MixedEmail,
                Otp = "654321",
                Purpose = OtpPurposeNames.PasswordReset
            }),
            CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(action);
        await cache.Received(1)
            .GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None);
        await cache.Received(1).SetAsync(
            lockoutKey,
            true,
            OtpSecurity.LockoutDuration,
            CancellationToken.None);
    }

    [Fact]
    public async Task VerifyOtp_WrongAttemptConsumesThenReinsertsIncrementedChallenge()
    {
        const string challengeOtp = "123456";
        var cache = Substitute.For<ICacheService>();
        var challengeKey = OtpSecurity.ChallengeKey(OtpPurpose.Signup, CanonicalEmail);
        var challenge = new OtpChallengeState(
            challengeOtp,
            0,
            DateTime.UtcNow.AddMinutes(5));
        cache.GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None)
            .Returns(challenge);
        var handler = new VerifyOtpCommandHandler(
            cache,
            Substitute.For<ICurrentUserService>());

        var action = () => handler.Handle(
            new VerifyOtpCommand(new VerifyOtpRequest
            {
                Email = MixedEmail,
                Otp = "654321",
                Purpose = OtpPurposeNames.Signup
            }),
            CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(action);
        await cache.Received(1)
            .GetAndRemoveAsync<OtpChallengeState>(challengeKey, CancellationToken.None);
        var setCall = Assert.Single(
            cache.ReceivedCalls(),
            call => call.GetMethodInfo().Name == nameof(ICacheService.SetAsync)
                && Equals(call.GetArguments()[0], challengeKey));
        var updatedChallenge = Assert.IsType<OtpChallengeState>(setCall.GetArguments()[1]);
        Assert.Equal(1, updatedChallenge.FailedAttempts);
        Assert.Equal(challengeOtp, updatedChallenge.Otp);
    }

    [Fact]
    public void OtpKeys_ArePurposeScopedAndDoNotExposeEmail()
    {
        var signupKey = OtpSecurity.ChallengeKey(OtpPurpose.Signup, CanonicalEmail);
        var resetKey = OtpSecurity.ChallengeKey(OtpPurpose.PasswordReset, CanonicalEmail);
        var verificationKey = OtpSecurity.VerifiedKey(
            OtpPurpose.Signup,
            CanonicalEmail,
            SignupVerificationTicket);

        Assert.NotEqual(signupKey, resetKey);
        Assert.DoesNotContain(CanonicalEmail, signupKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CanonicalEmail, resetKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            SignupVerificationTicket,
            verificationKey,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoogleLogin_LinksCanonicalStoredEmailWithoutCreatingDuplicate()
    {
        // Arrange
        var existingUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Existing User",
            Email = CanonicalEmail,
            IsActive = true,
            IsEmailVerified = true
        };
        var context = new InMemoryApplicationDbContext();
        var users = context.AddSet(existingUser);
        var googleAuth = Substitute.For<IGoogleAuthService>();
        googleAuth.VerifyAuthCodeAsync("auth-code", CancellationToken.None)
            .Returns(new GoogleUserInfoDTO
            {
                Email = MixedEmail,
                Name = "Google User",
                GoogleId = "google-subject"
            });
        var dateTime = Substitute.For<IDateTimeService>();
        dateTime.UtcNow.Returns(new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc));
        var jwt = Substitute.For<IJwtService>();
        jwt.GenerateRefreshToken().Returns("refresh-token");
        jwt.HashRefreshToken("refresh-token").Returns("refresh-token-hash");
        jwt.GetRefreshTokenExpiryMinutes().Returns(60);
        var handler = new GoogleLoginCommandHandler(
            context,
            googleAuth,
            jwt,
            dateTime,
            Substitute.For<IUserEloService>(),
            Substitute.For<IMapper>());

        // Act
        await handler.Handle(
            new GoogleLoginCommand("auth-code", null, true),
            CancellationToken.None);

        // Assert
        Assert.Same(existingUser, Assert.Single(users.Entities));
        Assert.Equal("refresh-token-hash", existingUser.RefreshTokenHash);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task GoogleSignup_StoresCanonicalEmail()
    {
        // Arrange
        var context = new InMemoryApplicationDbContext();
        var users = context.AddSet<User>();
        var googleAuth = Substitute.For<IGoogleAuthService>();
        googleAuth.VerifyAuthCodeAsync("auth-code", CancellationToken.None)
            .Returns(new GoogleUserInfoDTO
            {
                Email = MixedEmail,
                Name = "Google User",
                GoogleId = "google-subject"
            });
        var dateTime = Substitute.For<IDateTimeService>();
        dateTime.UtcNow.Returns(new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc));
        var jwt = Substitute.For<IJwtService>();
        jwt.GenerateRefreshToken().Returns("refresh-token");
        jwt.HashRefreshToken("refresh-token").Returns("refresh-token-hash");
        jwt.GetRefreshTokenExpiryMinutes().Returns(60);
        var handler = new GoogleLoginCommandHandler(
            context,
            googleAuth,
            jwt,
            dateTime,
            Substitute.For<IUserEloService>(),
            Substitute.For<IMapper>());

        // Act
        await handler.Handle(
            new GoogleLoginCommand("auth-code", (int)UserRole.Freelancer, false),
            CancellationToken.None);

        // Assert
        Assert.Equal(CanonicalEmail, Assert.Single(users.Entities).Email);
        Assert.Equal(1, context.SaveChangesCount);
    }
}
