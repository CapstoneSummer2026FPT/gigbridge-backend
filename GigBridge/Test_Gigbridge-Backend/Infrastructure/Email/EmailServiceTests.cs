using Application.Features.Auth.Shared.DTOs;
using Infrastructure.Services.Email;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Resend;

namespace Test_Gigbridge_Backend.Infrastructure.Email;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_UsesIdempotencyKeyAndPrivateByteAttachment()
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
                    "GigBridge-contract.docx",
                    content,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            ]
        });

        var call = Assert.Single(resend.ReceivedCalls());
        Assert.Equal("esign:document:client", call.GetArguments()[0]);
        var message = Assert.IsType<EmailMessage>(call.GetArguments()[1]);
        var attachment = Assert.Single(message.Attachments!);
        Assert.Equal("GigBridge-contract.docx", attachment.Filename);
        Assert.Equal(content, attachment.Content!.Value.AsByteArray());
    }
}
