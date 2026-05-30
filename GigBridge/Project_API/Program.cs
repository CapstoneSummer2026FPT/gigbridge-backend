using Application;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Services;
using Application.Features.Proposals.Services;
using Hangfire;
using Infrastructure;
using Infrastructure.Persistence;
using Project_API.Extensions;
using Project_API.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilterAttribute>();
});

// Layer registrations (Clean Architecture)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddScoped<IProposalsService, Infrastructure.Services.Proposals.ProposalsService>();
builder.Services.AddScoped<IJobPostsService, Infrastructure.Services.JobPosts.JobPostsService>();
// API-layer concerns
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithBearerAuth();
builder.Services.AddCorsPolicy();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, Project_API.Services.CurrentUserService>();
builder.Services.AddSignalR();
//builder.Services.AddHangfireServices(builder.Configuration);
builder.Services.AddHybridCache(builder.Configuration);

if (builder.Environment.IsEnvironment("Testing"))
{
    //builder.Services.AddHangfireServices(builder.Configuration);
}
builder.Services.AddHybridCache(builder.Configuration);

var app = builder.Build();

// Enable Swagger in all environments for testing
app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<GigbridgeDbContext>();
        DbSeeder.Seed(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seed failed: {ex.Message}");
    }
}

app.UseMiddleware<Project_API.Middleware.ExceptionHandlingMiddleware>();
app.UseMiddleware<Project_API.Middleware.RequestLoggingMiddleware>();

app.UseCors("AllowAll"); // CORS must be BEFORE UseHttpsRedirection and MapControllers
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<Project_API.Hubs.ChatHub>("/hubs/chat");
app.MapHub<Project_API.Hubs.NotificationHub>("/hubs/notification");

if (!app.Environment.IsEnvironment("Testing"))
{
    //app.UseHangfireDashboard();
}

app.Run();

public partial class Program;
