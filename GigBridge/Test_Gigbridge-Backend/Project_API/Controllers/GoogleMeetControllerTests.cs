//using Application.Common.Interfaces.IService;
//using Infrastructure.ExternalServices.GoogleMeet;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Options;
//using NSubstitute;
//using Project_API.Controllers.Integrations;

//namespace Test_Gigbridge_Backend.Project_API.Controllers;

//public class GoogleMeetControllerTests
//{
//    [Fact]
//    public void Callback_RedirectsOAuthParametersToConfiguredFrontendOrigin()
//    {
//        var controller = CreateController("https://app.gigbridge.test/integrations/google-meet/callback");

//        var result = Assert.IsType<RedirectResult>(controller.Callback("state value", "code/value", null));

//        Assert.Equal(
//            "https://app.gigbridge.test/integrations/google-meet/callback?result=processing" +
//            "&state=state%20value&code=code%2Fvalue",
//            result.Url);
//    }

//    [Fact]
//    public void Callback_WithoutState_RedirectsFailureToConfiguredFrontendOrigin()
//    {
//        var controller = CreateController("https://app.gigbridge.test/integrations/google-meet/callback");

//        var result = Assert.IsType<RedirectResult>(controller.Callback(null, null, null));

//        Assert.Equal(
//            "https://app.gigbridge.test/integrations/google-meet/callback?result=missing_state",
//            result.Url);
//    }

//    private static GoogleMeetController CreateController(string frontendCallbackUri)
//    {
//        var options = Options.Create(new GoogleMeetOptions
//        {
//            FrontendCallbackUri = frontendCallbackUri
//        });
//        return new GoogleMeetController(Substitute.For<IGoogleMeetOAuthService>(), options);
//    }
//}
