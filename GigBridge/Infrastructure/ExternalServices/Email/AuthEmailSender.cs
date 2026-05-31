using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Email;

public class AuthEmailSender : IAuthEmailSender
{
    private const string VerifyEmailTemplate = "VerifyEmail.html";
    private const string ResetPasswordTemplate = "ResetPassword.html";
    private const string OtpEmailTemplate = "OtpEmail.html";

    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration _configuration;

    public AuthEmailSender(
        IEmailService emailService,
        IWebHostEnvironment webHostEnvironment,
        IConfiguration configuration)
    {
        _emailService = emailService;
        _webHostEnvironment = webHostEnvironment;
        _configuration = configuration;
    }

    public async Task SendVerificationEmailAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var verifyLink = BuildFrontendUrl($"api/Auth/verify-email?token={Uri.EscapeDataString(token)}");
        var body = await RenderTemplateAsync(VerifyEmailTemplate, "{{VERIFICATION_URL}}", verifyLink, cancellationToken);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = email,
            Subject = "Welcome to GigBridge! Please Confirm Your Email",
            IsHtml = true
        }, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var resetLink = BuildFrontendUrl($"api/Auth/reset-password?token={Uri.EscapeDataString(token)}");
        var body = await RenderTemplateAsync(ResetPasswordTemplate, "{{RESET_URL}}", resetLink, cancellationToken);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = email,
            Subject = "GigBridge: Please Confirm Your New Password",
            IsHtml = true
        }, cancellationToken);
    }

    public async Task SendOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default)
    {
        var body = await RenderTemplateAsync(OtpEmailTemplate, "{{OTP_CODE}}", otp, cancellationToken);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = email,
            Subject = "GigBridge: Your Verification Code",
            IsHtml = true
        }, cancellationToken);
    }

    private string BuildFrontendUrl(string relativePath)
    {
        var frontendUrl = _configuration["FrontendBaseUrl"] ?? "https://localhost:7094";
        return $"{frontendUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private async Task<string> RenderTemplateAsync(
        string templateName,
        string tokenName,
        string tokenValue,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_webHostEnvironment.ContentRootPath, "Templates", templateName);
        var body = await File.ReadAllTextAsync(path, cancellationToken);
        return body.Replace(tokenName, tokenValue);
    }
}
