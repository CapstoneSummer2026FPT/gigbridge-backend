using Application.Common.Interfaces;
using Application.Common.Services;
using Application.Features.Contracts.Completion.Client.Commands;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common;

public sealed class ContractAutoCompletionWorkerTests
{
    [Fact]
    public async Task ProcessOnceAsync_EndsOnlyContractApprovedForAtLeastSeventyTwoHours()
    {
        var context = new InMemoryApplicationDbContext();
        var eligible = CreateContract(DateTime.UtcNow.AddHours(-73));
        var recent = CreateContract(DateTime.UtcNow.AddHours(-71));
        context.AddSet(eligible.Contract, recent.Contract);
        context.AddSet(eligible.Milestone, recent.Milestone);
        var mediator = Substitute.For<IMediator>();
        var services = new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(context)
            .AddSingleton(mediator)
            .BuildServiceProvider();
        var worker = new ContractAutoCompletionWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ContractAutoCompletionWorker>.Instance);

        await worker.ProcessOnceAsync(CancellationToken.None);

        var call = Assert.Single(mediator.ReceivedCalls());
        var command = Assert.IsType<EndProjectCommand>(call.GetArguments()[0]);
        Assert.Equal(eligible.Contract.ContractsId, command.ContractId);
        Assert.Equal(eligible.Contract.ClientProfiles.UserId, command.UserId);
    }

    private static (Contract Contract, Milestone Milestone) CreateContract(DateTime approvedAt)
    {
        var client = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };
        var contract = new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = client.ClientProfilesId,
            ClientProfiles = client,
            Title = "Auto-complete",
            Status = (int)ContractStatus.Active,
            CreatedAt = approvedAt.AddDays(-1)
        };
        var milestone = new Milestone
        {
            MilestonesId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            Status = (int)MilestoneStatus.Approved,
            ApprovedAt = approvedAt,
            CreatedAt = contract.CreatedAt
        };
        contract.Milestones.Add(milestone);
        return (contract, milestone);
    }
}
