using Application.Common.Interfaces.Email;
using Application.Common.InternalServices.Auth.Services;
using Application.Common.Models.Email;
using Application.Features.Auth.Shared.DTOs;
using Infrastructure.ExternalServices.Email.Resend;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Resend;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Email.Resend;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_UsesIdempotencyKeyAndFinalContractPdfAttachment()
    {
        var resend = Substitute.For<IResend>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = new EmailService(resend, configuration);
        var content = new byte[] { 1, 2, 3, 4 };
        await service.SendEmailAsync(new EmailRequest
        {
            To = "client@example.com",
            Subject = "Hợp đồng hoàn tất",
            Body = "Attached",
            IdempotencyKey = "esign:document:client",
            ByteAttachments =
            [
                new EmailByteAttachment(
                    "Gigbridge-Client-Freelancer-Contract.pdf",
                    content,
                    "application/pdf")
            ]
        });

        var call = Assert.Single(resend.ReceivedCalls());
        Assert.Equal("esign:document:client", call.GetArguments()[0]);
        var message = Assert.IsType<EmailMessage>(call.GetArguments()[1]);
        var attachment = Assert.Single(message.Attachments!);
        Assert.Equal("Gigbridge-Client-Freelancer-Contract.pdf", attachment.Filename);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(content, attachment.Content!.Value.AsByteArray());
    }
}

public sealed class AuthEmailSenderTests
{
    [Fact]
    public async Task SendIdentityVerificationOtpEmailAsync_UsesDedicatedVietnameseTemplate()
    {
        var emailService = Substitute.For<IEmailService>();
        var sender = new AuthEmailSender(
            emailService,
            TestTemplateReader.FromProjectTemplates());

        await sender.SendIdentityVerificationOtpEmailAsync(
            "member@example.com",
            "123456",
            CancellationToken.None);

        var call = Assert.Single(emailService.ReceivedCalls());
        var sentEmail = Assert.IsType<EmailRequest>(call.GetArguments()[0]);
        Assert.Equal("member@example.com", sentEmail.To);
        Assert.Equal("GigBridge: Xác thực mã định danh", sentEmail.Subject);
        Assert.True(sentEmail.IsHtml);
        Assert.Contains("Xác thực mã định danh", sentEmail.Body);
        Assert.Contains("123456", sentEmail.Body);
        Assert.Contains("5 phút", sentEmail.Body);
        Assert.DoesNotContain("{{OTP_CODE}}", sentEmail.Body);
    }
}
