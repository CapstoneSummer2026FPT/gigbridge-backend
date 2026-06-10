using System.Reflection;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class UpdateProposalStatusCommandHandlerTests
{
    [Fact]
    public void CreateContractIfMissing_AddsFixedPriceContractFromAcceptedProposal()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new CollectingDbContext();
        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));

        var jobPostId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var endDate = now.AddDays(14);

        var proposal = new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = freelancerProfileId,
            ProposedBudget = 1234m,
            JobPosts = new JobPost
            {
                JobPostsId = jobPostId,
                ClientProfilesId = clientProfileId,
                Title = "Fixed price job",
                Description = "Build the fixed price workflow.",
                Status = 1,
                CreatedAt = now,
                EndDate = endDate
            }
        };

        InvokeCreateContractIfMissing(handler, proposal);

        var contract = Assert.Single(context.Contracts.Entities);
        Assert.Equal(jobPostId, contract.JobPostsId);
        Assert.Equal(clientProfileId, contract.ClientProfilesId);
        Assert.Equal(freelancerProfileId, contract.FreelancerProfilesId);
        Assert.Equal(proposalId, contract.ProposalsId);
        Assert.Equal("Fixed price job", contract.Title);
        Assert.Equal("Build the fixed price workflow.", contract.Description);
        Assert.Equal(1234m, contract.TotalBudget);
        Assert.Equal(0, contract.Status);
        Assert.Equal(DateOnly.FromDateTime(now), contract.StartDate);
        Assert.Equal(DateOnly.FromDateTime(endDate), contract.EndDate);
        Assert.Equal(now, contract.CreatedAt);
    }

    [Fact]
    public void CreateContractIfMissing_RejectsAcceptedProposalWithoutBudget()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new CollectingDbContext();
        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));

        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = Guid.NewGuid(),
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = null,
            JobPosts = new JobPost
            {
                JobPostsId = Guid.NewGuid(),
                ClientProfilesId = Guid.NewGuid(),
                Title = "Fixed price job",
                Description = "Build the fixed price workflow.",
                Status = 1,
                CreatedAt = now
            }
        };

        var exception = Assert.Throws<TargetInvocationException>(
            () => InvokeCreateContractIfMissing(handler, proposal));

        Assert.IsType<BadRequestException>(exception.InnerException);
    }

    private static void InvokeCreateContractIfMissing(
        UpdateProposalStatusCommandHandler handler,
        Proposal proposal)
    {
        typeof(UpdateProposalStatusCommandHandler)
            .GetMethod("CreateContractIfMissing", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, new object[] { proposal });
    }

    private sealed class CollectingDbContext : IApplicationDbContext
    {
        public CollectingDbSet<Contract> Contracts { get; } = new();

        public DbSet<TEntity> Set<TEntity>() where TEntity : class
        {
            if (typeof(TEntity) == typeof(Contract))
            {
                return (DbSet<TEntity>)(object)Contracts;
            }

            throw new NotSupportedException($"Set<{typeof(TEntity).Name}> is not supported by this test context.");
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class CollectingDbSet<TEntity> : DbSet<TEntity>
        where TEntity : class
    {
        public List<TEntity> Entities { get; } = new();

        public override IEntityType EntityType => null!;

        public override EntityEntry<TEntity> Add(TEntity entity)
        {
            Entities.Add(entity);
            return null!;
        }
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
