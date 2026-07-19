using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.Common.Internal;

internal static class AdminDisputeSupport
{
    private static readonly Dictionary<int, string> ResolutionLabels = new()
    {
        [(int)DisputeResolution.ClientFavored] = "Client Favored",
        [(int)DisputeResolution.FreelancerFavored] = "Freelancer Favored",
        [(int)DisputeResolution.Split] = "Split",
        [(int)DisputeResolution.Dismissed] = "Dismissed"
    };

    public static string? GetResolutionLabel(int? resolution) =>
        resolution.HasValue ? ResolutionLabels.GetValueOrDefault(resolution.Value) : null;

    public static async Task EnsureAdminAsync(
        IApplicationDbContext context,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var isAdmin = await context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user =>
                    user.UserId == adminId &&
                    user.Role == (int)UserRole.Admin &&
                    user.IsActive,
                cancellationToken);

        if (!isAdmin)
            throw new ForbiddenAccessException("An active administrator account is required.");
    }

    public static async Task<AdminDisputeDetailResponse> GetDetailAsync(
        IApplicationDbContext context,
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var dispute = await context.Set<Dispute>()
            .AsNoTracking()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.ClientProfiles)
                    .ThenInclude(profile => profile.User)
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.FreelancerProfiles)
                    .ThenInclude(profile => profile!.User)
            .Include(item => item.Initiator)
            .Include(item => item.Milestones)
            .Include(item => item.DisputeEvidences)
            .FirstOrDefaultAsync(item => item.DisputesId == disputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        var clientProfile = dispute.Contracts.ClientProfiles;
        var freelancerProfile = dispute.Contracts.FreelancerProfiles;
        var client = new AdminDisputePartyResponse(
            clientProfile.UserId,
            clientProfile.ClientProfilesId,
            clientProfile.User.FullName,
            clientProfile.User.Email);
        var freelancer = freelancerProfile is null
            ? null
            : new AdminDisputePartyResponse(
                freelancerProfile.UserId,
                freelancerProfile.FreelancerProfilesId,
                freelancerProfile.User.FullName,
                freelancerProfile.User.Email);

        var initiatorRole = dispute.InitiatorId == client.UserId
            ? "Client"
            : freelancer?.UserId == dispute.InitiatorId ? "Freelancer" : null;

        return new AdminDisputeDetailResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.Contracts.Title,
            dispute.Contracts.Status,
            dispute.InitiatorId,
            dispute.Initiator.FullName,
            initiatorRole,
            client,
            freelancer,
            dispute.MilestonesId,
            dispute.Milestones?.Title,
            dispute.Reason,
            dispute.Status,
            dispute.Resolution,
            GetResolutionLabel(dispute.Resolution),
            dispute.ResolutionNote,
            dispute.ResolvedByAdminId,
            dispute.AssignedAdminId,
            dispute.AssignedAt,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            dispute.DisputeEvidences
                .OrderBy(evidence => evidence.CreatedAt)
                .Select(evidence => new DisputeEvidenceResponse(
                    evidence.DisputeEvidenceId,
                    evidence.UploadedById,
                    evidence.FileName,
                    evidence.FileSize,
                    evidence.Description,
                    evidence.CreatedAt))
                .ToList());
    }

    public static async Task NotifyParticipantsAsync(
        INotificationService notifications,
        ILogger logger,
        Contract contract,
        Dispute dispute,
        string content,
        CancellationToken cancellationToken)
    {
        var participantIds = new[]
            {
                contract.ClientProfiles.UserId,
                contract.FreelancerProfiles?.UserId
            }
            .Where(userId => userId.HasValue && userId.Value != Guid.Empty)
            .Select(userId => userId!.Value)
            .Distinct();

        foreach (var participantId in participantIds)
        {
            try
            {
                await notifications.CreateNotificationAsync(
                    participantId,
                    NotificationType.DisputeUpdate,
                    "Dispute case updated",
                    content,
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Dispute {DisputeId} was updated, but notification delivery to user {UserId} failed.",
                    dispute.DisputesId,
                    participantId);
            }
        }
    }
}
