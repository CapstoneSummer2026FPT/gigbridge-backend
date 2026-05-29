using Application.Common.Interfaces.IRepository;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class ProposalRepository : Repository<Proposal>, IProposalRepository
    {
        private readonly GigbridgeDbContext _context;

        public ProposalRepository(GigbridgeDbContext context) : base(context)
        {
            _context = context;
        }

        public void Update(Proposal proposal)
        {
            _context.Set<Proposal>().Update(proposal);
        }

        // Đã đổi tên biến để mapping đúng với Entity của bạn
        public async Task<bool> HasUserSubmittedProposalAsync(Guid jobPostsId, Guid freelancerProfilesId)
        {
            return await _context.Set<Proposal>()
                .AnyAsync(p =>
                    p.JobPostsId == jobPostsId &&
                    p.FreelancerProfilesId == freelancerProfilesId); // Đã đổi thành FreelancerProfilesId
        }
    }
}