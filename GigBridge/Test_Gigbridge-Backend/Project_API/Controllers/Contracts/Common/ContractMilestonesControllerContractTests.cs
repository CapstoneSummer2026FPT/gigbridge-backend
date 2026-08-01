using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Contracts.Common;

namespace Test_Gigbridge_Backend.Project_API.Controllers.Contracts.Common;

public sealed class ContractMilestonesControllerContractTests
{
    [Fact]
    public void Withdraw_ExposesFreelancerOnlyEarlyWithdrawalRoute()
    {
        var action = typeof(ContractMilestonesController)
            .GetMethod(nameof(ContractMilestonesController.Withdraw));

        Assert.NotNull(action);
        Assert.Equal(
            "{milestoneId:guid}/withdraw",
            action!.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal(
            "Freelancer",
            action.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
    }
}
