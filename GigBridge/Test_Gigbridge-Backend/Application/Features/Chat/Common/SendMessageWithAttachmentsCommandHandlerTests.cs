using System.IO.Compression;
using Application.Common.Exceptions;
using Application.Common.Interfaces.Media;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Chat.Common.Messages.SendWithAttachments.Commands;
using Domain.Enums.Chat;
using Infrastructure.Adapters.Files;
using MediatR;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Chat.Common;

public sealed class SendMessageWithAttachmentsCommandHandlerTests
{
    private static readonly byte[] ValidPdfContent =
        [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A];

    [Fact]
    public async Task Handle_UsesSharedPolicyAndForwardsAllValidatedAttachments()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mediaService = new CapturingMediaService();
        var sender = Substitute.For<ISender>();
        SendMessageCommand? forwardedCommand = null;
        sender.Send(
                Arg.Do<SendMessageCommand>(command => forwardedCommand = command),
                Arg.Any<CancellationToken>())
            .Returns(new MessageResponse(
                Guid.NewGuid(),
                conversationId,
                userId,
                (int)MessageType.File,
                null,
                null,
                null,
                "client-message-1",
                DateTime.UtcNow,
                null,
                false,
                []));
        var sourceArchive = CreateZipContent("src/index.ts", "export const value = 1;");
        var handler = new SendMessageWithAttachmentsCommandHandler(
            mediaService,
            sender,
            new WorkspaceUploadFilePolicy());

        await handler.Handle(
            new SendMessageWithAttachmentsCommand(
                conversationId,
                userId,
                "client-message-1",
                null,
                [
                    new ChatMessageFile(
                        new MemoryStream(ValidPdfContent),
                        "brief.pdf",
                        "application/pdf",
                        ValidPdfContent.Length),
                    new ChatMessageFile(
                        new MemoryStream(sourceArchive),
                        "source.zip",
                        "application/zip",
                        sourceArchive.Length)
                ]),
            CancellationToken.None);

        Assert.Equal(2, mediaService.UploadedFileNames.Count);
        Assert.NotNull(forwardedCommand);
        Assert.Equal(2, forwardedCommand!.Request.Attachments!.Count);
        Assert.All(
            forwardedCommand.Request.Attachments!,
            attachment => Assert.Equal(
                $"https://test-storage/{attachment.FileName}",
                attachment.FileUrl));
    }

    [Fact]
    public async Task Handle_RejectsExecutableInsideArchiveBeforeUploadingAnything()
    {
        var executableArchive = CreateZipContent(
            "tools/run.cmd",
            "@echo off\r\ncalc.exe");
        var mediaService = new CapturingMediaService();
        var handler = new SendMessageWithAttachmentsCommandHandler(
            mediaService,
            Substitute.For<ISender>(),
            new WorkspaceUploadFilePolicy());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SendMessageWithAttachmentsCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "client-message-2",
                null,
                [new ChatMessageFile(
                    new MemoryStream(executableArchive),
                    "unsafe.zip",
                    "application/zip",
                    executableArchive.Length)]),
            CancellationToken.None));

        Assert.Empty(mediaService.UploadedFileNames);
    }

    private static byte[] CreateZipContent(string fileName, string content)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(fileName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return output.ToArray();
    }

    private sealed class CapturingMediaService : IMediaService
    {
        public List<string> UploadedFileNames { get; } = [];

        public Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default)
        {
            UploadedFileNames.Add(fileName);
            return Task.FromResult($"https://test-storage/{fileName}");
        }

        public Task<string> UploadPrivateFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default) =>
            UploadFileAsync(fileStream, fileName, contentType, folder, cancellationToken);

        public Task DeleteFileAsync(
            string fileUrl,
            string expectedFolder,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetPrivateDownloadUrlAsync(
            string storageKey,
            string contentType,
            CancellationToken cancellationToken = default) => Task.FromResult(storageKey);

        public Task DeletePrivateFileAsync(
            string storageKey,
            string contentType,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
