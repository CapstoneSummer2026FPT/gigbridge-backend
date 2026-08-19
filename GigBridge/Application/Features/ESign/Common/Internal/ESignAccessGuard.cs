using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.Internal;

internal static class ESignAccessGuard
{
    public static async Task<JobPost> GetJobPostAsync(
        IApplicationDbContext context,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>()
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == jobPostId, cancellationToken);

        return jobPost ?? throw new NotFoundException("Job post does not exist.");
    }

    public static async Task<EsignDocument> GetDocumentAsync(
        IApplicationDbContext context,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await context.Set<EsignDocument>()
            .FirstOrDefaultAsync(
                document => document.EsignDocumentsId == documentId,
                cancellationToken);

        return document ?? throw new NotFoundException("E-sign document does not exist.");
    }

    public static async Task<EsignDocumentContent> GetContentAsync(
        IApplicationDbContext context,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var content = await context.Set<EsignDocumentContent>()
            .FirstOrDefaultAsync(
                content => content.EsignDocumentsId == documentId,
                cancellationToken);

        return content ?? throw new NotFoundException("E-sign document content does not exist.");
    }

    public static async Task EnsureOwningClientAsync(
        IApplicationDbContext context,
        JobPost jobPost,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var clientProfile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (clientProfile is null || clientProfile.ClientProfilesId != jobPost.ClientProfilesId)
        {
            throw new ForbiddenAccessException("Only the owning client can access this e-sign document.");
        }
    }

    public static async Task EnsureCanViewDocumentAsync(
        IApplicationDbContext context,
        EsignDocument document,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (document.ContractsId.HasValue)
        {
            await EnsureCanViewContractDocumentAsync(context, document.ContractsId.Value, userId, cancellationToken);
            return;
        }

        var jobPost = await GetJobPostAsync(context, document.JobPostsId, cancellationToken);
        await EnsureOwningClientAsync(context, jobPost, userId, cancellationToken);
    }

    private static async Task EnsureCanViewContractDocumentAsync(
        IApplicationDbContext context,
        Guid contractId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var contract = await context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == contractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        var isAdmin = await context.Set<User>()
            .AsNoTracking()
            .AnyAsync(
                user => user.UserId == userId && user.Role == (int)UserRole.Admin,
                cancellationToken);

        if (isAdmin)
        {
            return;
        }

        var isOwningClient = await context.Set<ClientProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isOwningClient)
        {
            return;
        }

        if (contract.FreelancerProfilesId.HasValue)
        {
            var isAttachedFreelancer = await context.Set<FreelancerProfile>()
                .AsNoTracking()
                .AnyAsync(
                    profile =>
                        profile.UserId == userId &&
                        profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);

            if (isAttachedFreelancer)
            {
                return;
            }
        }

        throw new ForbiddenAccessException("You do not have permission to view this e-sign document.");
    }
}
