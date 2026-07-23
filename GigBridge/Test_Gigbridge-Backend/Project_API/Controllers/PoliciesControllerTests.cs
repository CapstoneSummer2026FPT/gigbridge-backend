using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Project_API.Controllers.Public;

namespace Test_Gigbridge_Backend.Project_API.Controllers;

public sealed class PoliciesControllerTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"gigbridge-policy-{Guid.NewGuid()}");

    [Fact]
    public void Endpoint_IsPublicAndUsesExpectedRoute()
    {
        var controllerType = typeof(PoliciesController);
        var action = controllerType.GetMethod(nameof(PoliciesController.GetGigBridgeVietnamPolicy));

        Assert.NotNull(controllerType.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/policies", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("gigbridge-vn", action?.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public void GetGigBridgeVietnamPolicy_ReturnsVietnameseMarkdown()
    {
        var policiesDirectory = Path.Combine(_contentRoot, "Policies");
        Directory.CreateDirectory(policiesDirectory);
        var policyPath = Path.Combine(policiesDirectory, "GigBridge_Policy_VN.md");
        const string markdown = "# Bộ chính sách GigBridge\n\nPhiên bản 1.0-DATN";
        File.WriteAllText(policyPath, markdown, Encoding.UTF8);

        var result = Assert.IsType<PhysicalFileResult>(CreateController().GetGigBridgeVietnamPolicy());

        Assert.Equal("text/markdown; charset=utf-8", result.ContentType);
        Assert.Equal(markdown, File.ReadAllText(result.FileName, Encoding.UTF8));
    }

    [Fact]
    public void GetGigBridgeVietnamPolicy_ReturnsNotFound_WhenFileIsMissing()
    {
        var result = CreateController().GetGigBridgeVietnamPolicy();

        Assert.IsType<NotFoundResult>(result);
    }

    private PoliciesController CreateController()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(_contentRoot);
        return new PoliciesController(environment);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }
}
