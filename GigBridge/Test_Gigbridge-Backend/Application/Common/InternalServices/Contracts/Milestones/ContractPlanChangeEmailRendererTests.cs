using Application.Common.InternalServices.Contracts.Milestones.Email;
using Application.Common.InternalServices.Contracts.Models;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Contracts.Milestones;

public sealed class ContractPlanChangeEmailRendererTests
{
    [Fact]
    public void Render_IncludesProjectPlanChangeDetails()
    {
        var renderer = new ContractPlanChangeEmailRenderer(TestTemplateReader.FromProjectTemplates());
        var model = new ContractPlanChangeEmailModel(
            "Alice Client",
            "John Freelancer",
            "Design System",
            "Please revise the WBS and second milestone.",
            "https://gigbridge.example/contracts/contract-id");

        var result = renderer.Render(model);

        Assert.Equal("Project plan changes requested - Design System", result.Subject);
        Assert.Contains("Alice Client", result.HtmlBody);
        Assert.Contains("John Freelancer", result.HtmlBody);
        Assert.Contains("Design System", result.HtmlBody);
        Assert.Contains("Please revise the WBS and second milestone.", result.HtmlBody);
        Assert.Contains("https://gigbridge.example/contracts/contract-id", result.HtmlBody);
        Assert.DoesNotContain("{{", result.HtmlBody);
        Assert.Contains("Please revise the WBS and second milestone.", result.TextBody);
    }

    [Fact]
    public void Render_EncodesUserAuthoredHtml()
    {
        var renderer = new ContractPlanChangeEmailRenderer(TestTemplateReader.FromProjectTemplates());
        var model = new ContractPlanChangeEmailModel(
            "<b>Client</b>",
            "<script>Freelancer</script>",
            "<img src=x onerror=alert(1)>",
            "<script>alert(1)</script>",
            "https://gigbridge.example/contracts/id?a=1&b=2");

        var result = renderer.Render(model);

        Assert.DoesNotContain("<script>alert(1)</script>", result.HtmlBody);
        Assert.DoesNotContain("<img src=x", result.HtmlBody);
        Assert.Contains("&lt;script&gt;", result.HtmlBody);
        Assert.Contains("a=1&amp;b=2", result.HtmlBody);
    }
}
