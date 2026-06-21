using Domain.Enums;

namespace Domain.Entities;

public partial class User
{
    public void AttachProfileForRole(DateTime createdAt)
    {
        switch (Role.ToUserRole())
        {
            case UserRole.Client:
                AttachClientProfile(createdAt);
                return;

            case UserRole.Freelancer:
                AttachFreelancerProfile(createdAt);
                return;

            case UserRole.Admin:
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(Role), $"Unsupported user role: {Role}");
        }
    }

    private void AttachClientProfile(DateTime createdAt)
    {
        if (ClientProfile is not null)
        {
            return;
        }

        ClientProfile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = UserId,
            CreatedAt = createdAt
        };
    }

    private void AttachFreelancerProfile(DateTime createdAt)
    {
        if (FreelancerProfile is not null)
        {
            return;
        }

        FreelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = UserId,
            CreatedAt = createdAt
        };
    }
}
