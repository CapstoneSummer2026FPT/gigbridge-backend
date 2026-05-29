using Domain.Entities;
using System;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.IRepository
{
    public interface IProposalRepository : IRepository<Proposal>
    {
        void Update(Proposal proposal);

        Task<bool> HasUserSubmittedProposalAsync(Guid jobPostsId, Guid freelancerProfilesId);
    }
}