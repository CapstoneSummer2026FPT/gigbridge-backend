using Application.Common.Interfaces.Email;
using Application.Features.Auth.Common.Interfaces;
using Application.Common.Interfaces.Templates;
using Application.Features.Auth.Shared.DTOs;

namespace Infrastructure.Services.Email;

public class AuthEmailSender : IAuthEmailSender
{
    private const string OtpEmailTemplate = "Auth/Email/OtpEmail.html";
    private const string IdentityVerificationOtpEmailTemplate =
        "Auth/Email/IdentityVerificationOtpEmail.html";
    private const string ForgotPasswordOtpEmailTemplate = "Auth/Email/ForgotPasswordOtpEmail.html";

    private readonly IEmailService _emailService;
    private readonly ITemplateReader _templateReader;

    public AuthEmailSender(
        IEmailService emailService,
        ITemplateReader templateReader)
    {
        _emailService = emailService;
        _templateReader = templateReader;
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

    public async Task SendIdentityVerificationOtpEmailAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var body = await RenderTemplateAsync(
            IdentityVerificationOtpEmailTemplate,
            "{{OTP_CODE}}",
            otp,
            cancellationToken);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = email,
            Subject = "GigBridge: Xác thực mã định danh",
            IsHtml = true
        }, cancellationToken);
    }

    private async Task<string> RenderTemplateAsync(
        string templateName,
        string tokenName,
        string tokenValue,
        CancellationToken cancellationToken)
    {
        var body = await _templateReader.ReadTextAsync(templateName, cancellationToken);
        return body.Replace(tokenName, tokenValue);
    }
}
