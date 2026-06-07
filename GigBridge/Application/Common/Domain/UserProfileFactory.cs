using Domain.Entities;

namespace Application.Common.Domain;

internal static class UserProfileFactory
{
    public static void AttachProfileForRole(User user, DateTime createdAt)
    {
        if (user.Role == 0)
        {
            user.ClientProfile = new ClientProfile
            {
                ClientProfilesId = Guid.NewGuid(),
                CreatedAt = createdAt
            };
            return;
        }

        if (user.Role == 1)
        {
            user.FreelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = Guid.NewGuid(),
                CreatedAt = createdAt
            };
        }
    }
}
