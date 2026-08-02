using Application;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Infrastructure;
using Project_API.Extensions;
using Project_API.Hubs;
using Project_API.Middleware;
using Project_API.Security;
using Project_API.Services;
using Project_API.Services.Chat;
using Project_API.Services.Notification;
using Project_API.Services.SystemTracking;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

builder.Services.AddControllers();

// Layer registrations (Clean Architecture)
builder.Services.AddApplicationServices(builder.Configuration); 
builder.Services.AddInfrastructureServices(builder.Configuration);

// API-layer concerns
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithBearerAuth();
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);
builder.Services.AddAuthRateLimiting();
builder.Services.AddTrustedProxyForwarding();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IChatRealtimeNotifier, SignalRChatRealtimeNotifier>();
builder.Services.AddScoped<INotificationSender, SignalRNotificationSender>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<SystemTrackingStore>();
builder.Services.AddSingleton<ISystemTrackingReader>(provider => provider.GetRequiredService<SystemTrackingStore>());

builder.Services.AddHybridCache(builder.Configuration);

var app = builder.Build();

await app.EnsureLocalESignTemplatesAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors(Project_API.Extensions.ServiceCollectionExtensions.FrontendCorsPolicy);
app.UseRateLimiter();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<SystemTrackingHub>("/hubs/system-tracking");


app.Run();

public partial class Program;
