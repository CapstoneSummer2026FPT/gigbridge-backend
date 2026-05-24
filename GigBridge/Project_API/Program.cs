using Application;
using Application.Common.Interfaces.IService;
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

// API-layer concerns
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithBearerAuth();
builder.Services.AddCorsPolicy();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, Project_API.Services.CurrentUserService>();
builder.Services.AddSignalR();
builder.Services.AddHangfireServices(builder.Configuration);
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAll"); // CORS must be BEFORE MapControllers

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<Project_API.Hubs.ChatHub>("/hubs/chat");
app.MapHub<Project_API.Hubs.NotificationHub>("/hubs/notification");

app.UseHangfireDashboard();

app.Run();
