using Application.Common.Interfaces;
using Application.Common.InternalServices.Contracts.Completion.BackgroundJobs;
using Application.Features.Contracts.Completion.Client.Commands;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Infrastructure.Persistence;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Contracts.Completion;

public sealed class ContractAutoCompletionWorkerTests
{
    [Fact]
    public void CandidateQuery_TranslatesFinancialGuardsForPostgres()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseNpgsql("Host=localhost;Database=query_shape;Username=query_shape;Password=query_shape")
            .Options;
        using var context = new GigbridgeDbContext(options);

        var sql = ContractAutoCompletionWorker
            .CandidateQuery(context, DateTime.UtcNow.AddHours(-72))
            .ToQueryString();

        Assert.Contains("Contract.AutoCompletion.Candidates", sql, StringComparison.Ordinal);
        Assert.Contains("ContractEscrows", sql, StringComparison.Ordinal);
        Assert.Contains("UserWallets", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessOnceAsync_EndsOnlyContractApprovedForAtLeastSeventyTwoHours()
    {
        var context = new InMemoryApplicationDbContext();
        var eligible = CreateContract(DateTime.UtcNow.AddHours(-73));
        var recent = CreateContract(DateTime.UtcNow.AddHours(-71));
        var fundingDrift = CreateContract(DateTime.UtcNow.AddHours(-73));
        fundingDrift.Escrow.FundedAmount -= 1m;
        var compositionDrift = CreateContract(DateTime.UtcNow.AddHours(-73));
        compositionDrift.Escrow.DepositedTokens -= 1m;
        var insufficientHeldBalance = CreateContract(DateTime.UtcNow.AddHours(-73));
        insufficientHeldBalance.Wallet.HeldTokens = 0m;
        context.AddSet(
            eligible.Contract,
            recent.Contract,
            fundingDrift.Contract,
            compositionDrift.Contract,
            insufficientHeldBalance.Contract);
        context.AddSet(
            eligible.Milestone,
            recent.Milestone,
            fundingDrift.Milestone,
            compositionDrift.Milestone,
            insufficientHeldBalance.Milestone);
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

    private static (Contract Contract, Milestone Milestone, ContractEscrow Escrow, UserWallet Wallet) CreateContract(
        DateTime approvedAt)
    {
        const decimal amount = 100m;
        var wallet = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HeldTokens = amount
        };
        var user = new User
        {
            UserId = wallet.UserId,
            UserWallet = wallet
        };
        var client = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user
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
            Amount = amount,
            ApprovedAt = approvedAt,
            CreatedAt = contract.CreatedAt
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            RequiredAmount = amount,
            FundedAmount = amount,
            DepositedTokens = amount,
            Status = (int)ContractEscrowStatus.Funded,
            Contract = contract,
            CreatedAt = contract.CreatedAt
        };
        contract.Milestones.Add(milestone);
        contract.ContractEscrow = escrow;
        return (contract, milestone, escrow, wallet);
    }
}
