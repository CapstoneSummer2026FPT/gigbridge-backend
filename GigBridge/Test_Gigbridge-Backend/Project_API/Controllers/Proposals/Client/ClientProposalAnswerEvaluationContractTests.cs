using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Proposals.Client;

namespace Test_Gigbridge_Backend.Project_API.Controllers.Proposals.Client;

public class ClientProposalAnswerEvaluationContractTests
{
    [Fact]
    public void Controller_ExposesAnswerEvaluationRoute_WithoutLegacyInterviewRoute()
    {
        var routes = typeof(ClientProposalsController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true))
            .Cast<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("{proposalId}/answer-evaluation", routes);
        Assert.DoesNotContain("{proposalId}/ai-interview-judging", routes);
    }
}
