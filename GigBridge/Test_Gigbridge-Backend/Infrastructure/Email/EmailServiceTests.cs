using Application.Features.Auth.Shared.DTOs;
using Infrastructure.Services.Email;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Resend;

namespace Test_Gigbridge_Backend.Infrastructure.Email;

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
