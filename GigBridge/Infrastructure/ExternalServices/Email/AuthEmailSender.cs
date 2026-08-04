using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;

namespace Infrastructure.Services.Email;

public class AuthEmailSender : IAuthEmailSender
{
    private const string OtpEmailTemplate = "OtpEmail.html";
    private const string ForgotPasswordOtpEmailTemplate = "ForgotPasswordOtpEmail.html";

    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AuthEmailSender(
        IEmailService emailService,
        IWebHostEnvironment webHostEnvironment)
    {
        _emailService = emailService;
        _webHostEnvironment = webHostEnvironment;
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

    public async Task SendForgotPasswordOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default)
    {
        var body = await RenderTemplateAsync(ForgotPasswordOtpEmailTemplate, "{{OTP_CODE}}", otp, cancellationToken);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = email,
            Subject = "GigBridge: Reset Password Verification Code",
            IsHtml = true
        }, cancellationToken);
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
