using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;


namespace Project_API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "Frontend";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var signingKey = GetRequiredJwtSetting(jwtSettings, "Key");
        var issuer = GetRequiredJwtSetting(jwtSettings, "Issuer");
        var audience = GetRequiredJwtSetting(jwtSettings, "Audience");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes.");
        }

        var secretKey = Encoding.UTF8.GetBytes(signingKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(secretKey),

                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        return services;

    }

    public static IServiceCollection AddSwaggerWithBearerAuth(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "GigBridge API",
                Version = "v1"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter: Bearer {your JWT token}"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://gigbridge.id.vn",
            "https://www.gigbridge.id.vn"
        };

        var configuredOrigins = configuration["Cors:AllowedOrigins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];
        var allowLocalhost = environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Testing");

        foreach (var configuredOrigin in configuredOrigins.Concat(
                     configuration.GetSection("Cors:AllowedOrigins")
                         .GetChildren()
                         .Select(child => child.Value)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value!)))
        {
            if (!TryNormalizeOrigin(configuredOrigin, out var normalizedOrigin, out var configuredUri)
                || (configuredUri.Scheme != Uri.UriSchemeHttps
                    && !(allowLocalhost
                         && configuredUri.Scheme == Uri.UriSchemeHttp
                         && configuredUri.IsLoopback)))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contains an invalid or insecure origin: '{configuredOrigin}'.");
            }

            allowedOrigins.Add(normalizedOrigin);
        }

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
                policy.SetIsOriginAllowed(origin =>
                {
                    if (!TryNormalizeOrigin(origin, out var normalizedOrigin, out var uri))
                    {
                        return false;
                    }

                    return (allowLocalhost
                            && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                        || allowedOrigins.Contains(normalizedOrigin);
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
          );
        });

        return services;
    }

    private static string GetRequiredJwtSetting(IConfigurationSection jwtSettings, string key)
    {
        var value = jwtSettings[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Jwt:{key} must be configured.");
        }

        return value;
    }

    private static bool TryNormalizeOrigin(string origin, out string normalizedOrigin, out Uri uri)
    {
        normalizedOrigin = string.Empty;
        uri = null!;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsedUri)
            || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(parsedUri.UserInfo)
            || parsedUri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(parsedUri.Query)
            || !string.IsNullOrEmpty(parsedUri.Fragment))
        {
            return false;
        }

        uri = parsedUri;
        normalizedOrigin = parsedUri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
