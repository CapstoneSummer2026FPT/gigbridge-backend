using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Proposals.Client;

namespace Test_Gigbridge_Backend.Project_API.Controllers.Proposals.Client;

public class ClientProposalAnswerEvaluationContractTests
{
    [Fact]
    public void Controller_ExposesAiInterviewJudgingRoute_WithoutAnswerEvaluationRoute()
    {
        var routes = typeof(ClientProposalsController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true))
            .Cast<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("{proposalId}/ai-interview-judging", routes);
        Assert.DoesNotContain("{proposalId}/answer-evaluation", routes);
    }
}
