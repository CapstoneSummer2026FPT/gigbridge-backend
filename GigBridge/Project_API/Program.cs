using Application;
using Application.Common.Interfaces.IService;
using Infrastructure;
using Infrastructure.Persistence;
using Project_API.Extensions;
using Project_API.Services.Notification;

var builder = WebApplication.CreateBuilder(args);


// Layer registrations (Clean Architecture)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// API-layer concerns
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithBearerAuth();
builder.Services.AddCorsPolicy();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, Project_API.Services.CurrentUserService>();
builder.Services.AddScoped<INotificationSender, SignalRNotificationSender>();
builder.Services.AddSignalR();

if (!builder.Environment.IsEnvironment("Testing"))
{
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
        await DbSeeder.SeedLocalOnlyAsync<GigbridgeDbContext>(app.Services);
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
}

app.Run();

public partial class Program;
