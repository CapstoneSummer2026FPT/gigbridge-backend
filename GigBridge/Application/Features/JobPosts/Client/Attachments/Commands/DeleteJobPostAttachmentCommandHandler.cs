using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Attachments.Commands;

public sealed class DeleteJobPostAttachmentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteJobPostAttachmentCommand, bool>
{
    public async Task<bool> Handle(
        DeleteJobPostAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        var clientProfileId = await context.Set<ClientProfile>()
            .Where(profile => profile.UserId == request.UserId)
            .Select(profile => (Guid?)profile.ClientProfilesId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Client profile does not exist.");

        var attachment = await context.Set<JobPostAttachment>()
            .Include(item => item.JobPosts)
            .FirstOrDefaultAsync(
                item => item.JobPostAttachmentsId == request.AttachmentId &&
                        item.JobPostsId == request.JobPostId &&
                        item.JobPosts.ClientProfilesId == clientProfileId,
                cancellationToken)
            ?? throw new NotFoundException("Job post image does not exist or you do not have permission to delete it.");

        if (attachment.JobPosts.Visibility == 3)
            throw new BadRequestException("This job post has been locked by an admin and cannot be updated.");

        context.Set<JobPostAttachment>().Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
