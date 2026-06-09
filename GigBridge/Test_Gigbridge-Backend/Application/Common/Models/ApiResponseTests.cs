using Application.Common.Models;

namespace Test_Gigbridge_Backend.Application.Common.Models;

public class ApiResponseTests
{
    [Fact]
    public void Ok_ReturnsSuccessfulResponseWithData()
    {
        var data = new { Id = 1 };

        var response = ApiResponse<object>.Ok(data, "Loaded");

        Assert.True(response.Success);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Loaded", response.Message);
        Assert.Same(data, response.Data);
    }

    [Fact]
    public void BadRequest_ReturnsErrors()
    {
        var errors = new { Email = "Email is required." };

        var response = ApiResponse<object>.BadRequest("Invalid request", errors);

        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Same(errors, response.Errors);
    }

    [Fact]
    public void NoContent_ReturnsNoData()
    {
        var response = ApiResponse<object>.NoContent();

        Assert.True(response.Success);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(response.Data);
    }
}
