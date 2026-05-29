using Application.Common.Interfaces.IRepository;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class JobPostRepository : Repository<JobPost>, IJobPostRepository
    {
        private readonly GigbridgeDbContext _context;

        public JobPostRepository(GigbridgeDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<JobPost?> GetJobPostWithDetailsAsync(Guid jobId)
        {
            return await _context.Set<JobPost>()
                .Include(x => x.JobPostSkills)
                    .ThenInclude(js => js.Skills)
                .Include(x => x.JobPostAttachments)
                .Include(x => x.Proposals)
                .FirstOrDefaultAsync(x => x.JobPostsId == jobId);
        }

        public void Update(JobPost jobPost)
        {
            _context.Set<JobPost>().Update(jobPost);
        }

        public async Task<IEnumerable<JobPost>> GetAppliedJobPostsByFreelancerAsync(
            Guid freelancerId,
            int pageIndex = 1,
            int pageSize = 10)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            int skip = (pageIndex - 1) * pageSize;

            return await _context.Set<JobPost>()
                .Include(x => x.JobPostSkills)
                    .ThenInclude(js => js.Skills)
                .Where(j => j.Proposals.Any(p => p.FreelancerProfilesId == freelancerId))
                .OrderByDescending(j => j.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<(IEnumerable<JobPost> Items, int TotalCount)> GetAllPagedWithTotalAsync(
            int pageIndex,
            int pageSize,
            Expression<Func<JobPost, bool>>? filter = null,           
            string includeProperties = "",
            Func<JobPost, object>? orderBy = null,
            bool descending = true)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Set<JobPost>().AsQueryable();

            // Apply filter
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // Include properties
            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var include in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(include.Trim());
                }
            }

            int totalCount = await query.CountAsync();

            // Order By
            if (orderBy != null)
            {
                query = (IQueryable<JobPost>)(descending
                    ? query.OrderByDescending(orderBy)
                    : query.OrderBy(orderBy));
            }
            else
            {
                query = query.OrderByDescending(j => j.CreatedAt);
            }

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}