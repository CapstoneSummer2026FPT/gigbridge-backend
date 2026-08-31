using Application;
using Application.Common.Interfaces.Identity;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Infrastructure;
using Project_API;
using Project_API.Extensions;
using Project_API.Hubs;
using Project_API.Middleware;
using Project_API.Security;
using Project_API.Services;
using Project_API.Services.Chat;
using Project_API.Services.Notification;
using Project_API.Services.SystemTracking;

// Multi-node load balancing enabled with Redis SignalR Backplane
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.ValidateAuthSessionConfiguration(builder.Environment);

builder.Services.AddScoped<IRequestMetadataAccessor, Project_API.Services.RequestMetadataAccessor>();
builder.WebHost.UseInfrastructureMonitoring(builder.Configuration, builder.Environment);

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

builder.Services.AddControllers();

// Layer registrations
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
builder.Services.AddScoped<IUserRealtimeEventSender, SignalRChatRealtimeNotifier>();
builder.Services.AddScoped<INotificationSender, SignalRNotificationSender>();

var signalRBuilder = builder.Services.AddSignalR();
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectTimeout = 10000;
    redisOptions.KeepAlive = 60;
    redisOptions.SyncTimeout = 10000;

    var redisConnection = StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions);

    signalRBuilder.AddStackExchangeRedis(options =>
    {
        options.ConnectionFactory = async writer => await Task.FromResult(redisConnection);
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("GigBridge_SignalR");
    });
}
else
{
    using var startupLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
    startupLoggerFactory.CreateLogger("Startup")
        .LogWarning("Redis connection string is empty or missing! SignalR backplane disabled.");
}

builder.Services.AddSingleton<SystemTrackingStore>();
builder.Services.AddSingleton<ISystemTrackingReader>(provider => provider.GetRequiredService<SystemTrackingStore>());

var app = builder.Build();

await app.EnsureLocalESignTemplatesAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

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
