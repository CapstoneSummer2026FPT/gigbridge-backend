using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Project_API.Controllers.Profiles.Freelancer;

namespace Test_Gigbridge_Backend.Project_API.Controllers;

public sealed class WorkExperienceControllerContractTests
{
    [Fact]
    public void Controller_ExposesDedicatedWorkExperienceCrudRoutes()
    {
        var controllerRoute = Assert.Single(
            typeof(WorkExperienceController).GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>(),
            route => route.Template == "api/work-experience");

        Assert.Equal("api/work-experience", controllerRoute.Template);
        AssertActionRoute<HttpGetAttribute>(
            nameof(WorkExperienceController.GetMyWorkExperiences), "me");
        AssertActionRoute<HttpGetAttribute>(
            nameof(WorkExperienceController.GetWorkExperiences), "user/{userId:guid}");
        AssertActionRoute<HttpPostAttribute>(
            nameof(WorkExperienceController.CreateWorkExperience), null);
        AssertActionRoute<HttpPutAttribute>(
            nameof(WorkExperienceController.UpdateWorkExperience), "{workExperienceId:guid}");
        AssertActionRoute<HttpDeleteAttribute>(
            nameof(WorkExperienceController.DeleteWorkExperience), "{workExperienceId:guid}");
    }

    [Theory]
    [InlineData(nameof(WorkExperienceController.CreateWorkExperience))]
    [InlineData(nameof(WorkExperienceController.UpdateWorkExperience))]
    [InlineData(nameof(WorkExperienceController.DeleteWorkExperience))]
    public void MutationActions_AreRestrictedToFreelancers(string actionName)
    {
        var action = typeof(WorkExperienceController).GetMethod(actionName);
        Assert.NotNull(action);

        var authorize = Assert.Single(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Freelancer), authorize.Roles);
    }

    [Fact]
    public void MyWorkExperiencesRead_IsRestrictedToFreelancers()
    {
        var action = typeof(WorkExperienceController)
            .GetMethod(nameof(WorkExperienceController.GetMyWorkExperiences));
        Assert.NotNull(action);

        var authorize = Assert.Single(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Freelancer), authorize.Roles);
    }

    private static void AssertActionRoute<TAttribute>(string actionName, string? template)
        where TAttribute : HttpMethodAttribute
    {
        var action = typeof(WorkExperienceController).GetMethod(actionName);
        Assert.NotNull(action);
        var route = Assert.Single(
            action.GetCustomAttributes(typeof(TAttribute), true).Cast<TAttribute>());
        Assert.Equal(template, route.Template);
    }
}
