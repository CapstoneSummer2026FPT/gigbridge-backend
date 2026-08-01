using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Project_API.Controllers.Jobs.Client;
using Swashbuckle.AspNetCore.Swagger;

namespace Test_Gigbridge_Backend.Project_API.Controllers.Jobs.Client;

public sealed class ClientJobPostsSwaggerContractTests
{
    [Fact]
    public void SwaggerDocument_DescribesJobPostAttachmentAsMultipartFile()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(ClientJobPostsController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "GigBridge API contract tests",
                Version = "v1"
            }));

        using var application = builder.Build();
        var swaggerProvider = application.Services.GetRequiredService<ISwaggerProvider>();

        var document = swaggerProvider.GetSwagger("v1");
        var operation = document.Paths["/api/JobPosts/{jobPostId}/attachments"]
            .Operations[OperationType.Post];

        var multipart = operation.RequestBody.Content["multipart/form-data"];
        Assert.Equal("object", multipart.Schema.Type);
        Assert.Equal("string", multipart.Schema.Properties["file"].Type);
        Assert.Equal("binary", multipart.Schema.Properties["file"].Format);
    }
}
