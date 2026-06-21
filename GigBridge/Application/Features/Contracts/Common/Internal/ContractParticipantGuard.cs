using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractParticipantGuard
{
    public static async Task<ClientProfile> EnsureClientAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var clientProfile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (clientProfile is null || clientProfile.ClientProfilesId != contract.ClientProfilesId)
        {
            throw new ForbiddenAccessException("Only the owning client can perform this contract action.");
        }

        return clientProfile;
    }

    public static async Task<FreelancerProfile> EnsureFreelancerAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!contract.FreelancerProfilesId.HasValue)
        {
            throw new BadRequestException("Contract does not have a selected freelancer.");
        }

        var freelancerProfile = await context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (freelancerProfile is null ||
            freelancerProfile.FreelancerProfilesId != contract.FreelancerProfilesId.Value)
        {
            throw new ForbiddenAccessException("Only the selected freelancer can perform this contract action.");
        }

        return freelancerProfile;
    }
}
