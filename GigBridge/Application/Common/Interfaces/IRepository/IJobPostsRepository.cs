using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.IRepository
{
    public interface IJobPostRepository : IRepository<JobPost>
    {
        Task<JobPost?> GetJobPostWithDetailsAsync(Guid jobPostsId);

        void Update(JobPost jobPost);

        Task<IEnumerable<JobPost>> GetAppliedJobPostsByFreelancerAsync(
            Guid freelancerId,
            int pageIndex = 1,
            int pageSize = 10);

        Task<(IEnumerable<JobPost> Items, int TotalCount)> GetAllPagedWithTotalAsync(
            int pageIndex,
            int pageSize,
            Expression<Func<JobPost, bool>>? filter = null,
            string includeProperties = "",
            Func<JobPost, object>? orderBy = null,
            bool descending = true);
    }
}