using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Project_API.Controllers.Profiles.Freelancer;

namespace Test_Gigbridge_Backend.Project_API.Controllers;

public sealed class PortfolioControllerContractTests
{
    [Fact]
    public void Controller_ExposesDedicatedPortfolioCrudRoutes()
    {
        var controllerRoute = Assert.Single(
            typeof(PortfolioController).GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>(),
            route => route.Template == "api/portfolio");

        Assert.Equal("api/portfolio", controllerRoute.Template);
        AssertActionRoute<HttpGetAttribute>(nameof(PortfolioController.GetMyPortfolio), "me");
        AssertActionRoute<HttpGetAttribute>(nameof(PortfolioController.GetPortfolio), "user/{userId:guid}");
        AssertActionRoute<HttpPostAttribute>(nameof(PortfolioController.CreatePortfolioItem), null);
        AssertActionRoute<HttpPutAttribute>(nameof(PortfolioController.UpdatePortfolioItem), "{portfolioItemId:guid}");
        AssertActionRoute<HttpDeleteAttribute>(nameof(PortfolioController.DeletePortfolioItem), "{portfolioItemId:guid}");
    }

    [Theory]
    [InlineData(nameof(PortfolioController.CreatePortfolioItem))]
    [InlineData(nameof(PortfolioController.UpdatePortfolioItem))]
    [InlineData(nameof(PortfolioController.DeletePortfolioItem))]
    public void MutationActions_AreRestrictedToFreelancers(string actionName)
    {
        var action = typeof(PortfolioController).GetMethod(actionName);
        Assert.NotNull(action);

        var authorize = Assert.Single(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Freelancer), authorize.Roles);
    }

    private static void AssertActionRoute<TAttribute>(string actionName, string? template)
        where TAttribute : HttpMethodAttribute
    {
        var action = typeof(PortfolioController).GetMethod(actionName);
        Assert.NotNull(action);
        var route = Assert.Single(
            action.GetCustomAttributes(typeof(TAttribute), true).Cast<TAttribute>());
        Assert.Equal(template, route.Template);
    }
}
