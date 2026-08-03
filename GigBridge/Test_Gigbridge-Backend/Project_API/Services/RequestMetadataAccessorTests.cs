using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Project_API.Services;

namespace Test_Gigbridge_Backend.Project_API.Services;

public sealed class RequestMetadataAccessorTests
{
    [Fact]
    public void CorrelationId_IsStableForRequest_AndHonorsValidGuidHeader()
    {
        var expected = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = expected.ToString();
        var accessor = new RequestMetadataAccessor(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(expected, accessor.CorrelationId);
        Assert.Equal(expected, accessor.CorrelationId);
    }

    [Fact]
    public void UserAgent_IsSanitizedAndLengthLimited()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = $"Browser\u0001Agent {new string('x', 600)}";
        var accessor = new RequestMetadataAccessor(new HttpContextAccessor { HttpContext = context });

        Assert.NotNull(accessor.UserAgent);
        Assert.DoesNotContain('\u0001', accessor.UserAgent!);
        Assert.True(accessor.UserAgent!.Length <= 512);
    }

    [Fact]
    public void AdminAuditLog_DoesNotExposeAnIpAddressProperty()
    {
        Assert.Null(typeof(AdminAuditLog).GetProperty("IpAddress"));
        Assert.NotNull(typeof(AdminAuditLog).GetProperty("CorrelationId"));
    }
}
