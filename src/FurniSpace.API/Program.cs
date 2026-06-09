using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FurniSpace.API.Constants;
using FurniSpace.Application;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.API.Middleware;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Logging;
using FurniSpace.Shared.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

EnvLoader.LoadEnv(required: false);

const string AllowAllCorsPolicy = "AllowAllCors";
const string WildcardCorsOrigin = "*";
const string CorsAllowedOriginsEnvKey = "CORS_ALLOWED_ORIGINS";

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = LoadJwtSettings(builder.Configuration);

Log.Logger = SerilogConfiguration.CreateLogger(
    builder.Configuration,
    useJsonFormatting: !builder.Environment.IsDevelopment());

builder.Host.UseSerilog();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
ConfigureForwardedHeaders(builder.Services, builder.Configuration);
AddAllowAllCors(builder.Services, builder.Configuration);
AddPublicAuthRateLimiter(builder.Services);
AddApiSwagger(builder.Services);
builder.Services.AddApplication(builder.Configuration);
AddJwtAuthentication(builder.Services, jwtSettings, builder.Environment);
builder.Services.AddAuthorization();

var app = builder.Build();

await MigrateAndSeedDatabaseAsync(app);
UseDevelopmentSwagger(app);
app.UseForwardedHeaders();
UseProductionHttps(app);
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.UseCors(AllowAllCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "FurniSpace API");
await app.RunAsync();
await Log.CloseAndFlushAsync();

static JwtSettings LoadJwtSettings(IConfiguration configuration)
{
    var settings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
    if (string.IsNullOrWhiteSpace(settings.SecretKey))
    {
        settings.SecretKey = configuration["JWT_SECRET"] ?? string.Empty;
    }

    _ = settings.GetSecretKeyBytes();
    return settings;
}

static Task ValidateAccessTokenAsync(TokenValidatedContext context)
{
    var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
    var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var issuedAtValue = context.Principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;

    if (string.IsNullOrWhiteSpace(jti) ||
        !Guid.TryParse(userIdValue, out var userId) ||
        !long.TryParse(issuedAtValue, out var issuedAtUnixSeconds))
    {
        context.Fail("Access token is missing required security claims.");
        return Task.CompletedTask;
    }

    return CheckAccessTokenRevocationAsync(context, jti, userId, issuedAtUnixSeconds);
}

static async Task CheckAccessTokenRevocationAsync(
    TokenValidatedContext context,
    string jti,
    Guid userId,
    long issuedAtUnixSeconds)
{
    var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnixSeconds);
    var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
    if (await authService.IsAccessTokenRevokedAsync(jti, userId, issuedAt))
    {
        context.Fail("Access token has been revoked.");
    }
}

static async Task MigrateAndSeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        await DataSeeder.SeedAsync(dbContext);
    }
    catch (Exception exception)
    {
        Log.Error(exception, "Failed to apply database migrations during startup.");
    }
}

static void ConfigureForwardedHeaders(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        AddKnownProxies(options, configuration);

        if (configuration.GetValue<bool>("ForwardedHeaders:TrustAll"))
        {
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        }
    });
}

static void AddKnownProxies(ForwardedHeadersOptions options, IConfiguration configuration)
{
    foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
}

static void AddPublicAuthRateLimiter(IServiceCollection services)
{
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth-public", httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    });
}

static void AddAllowAllCors(IServiceCollection services, IConfiguration configuration)
{
    var allowedOrigins = ResolveAllowedCorsOrigins(configuration);

    services.AddCors(options =>
    {
        options.AddPolicy(AllowAllCorsPolicy, policy =>
        {
            policy
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();

            if (allowedOrigins.Contains(WildcardCorsOrigin, StringComparer.Ordinal))
            {
                // API uses bearer tokens, not CORS credentials; wildcard is intentional for local/mobile clients.
                policy.SetIsOriginAllowed(_ => true);
                return;
            }

            policy.WithOrigins(allowedOrigins);
        });
    });
}

static string[] ResolveAllowedCorsOrigins(IConfiguration configuration)
{
    var envOrigins = configuration[CorsAllowedOriginsEnvKey];
    if (!string.IsNullOrWhiteSpace(envOrigins))
    {
        return envOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    return configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [WildcardCorsOrigin];
}

static void AddApiSwagger(IServiceCollection services)
{
    services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "FurniSpace API", Version = "v1", Description = "FurniSpace Backend API" });
        options.MapType<DateOnly>(() => new Microsoft.OpenApi.Models.OpenApiSchema
        {
            Type = "string",
            Format = "date",
            Example = new Microsoft.OpenApi.Any.OpenApiString("2026-08-15")
        });
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Please enter a valid JWT token",
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                SwaggerConstants.EmptySecurityScopes
            }
        });
    });
}

static void AddJwtAuthentication(IServiceCollection services, JwtSettings jwtSettings, IWebHostEnvironment environment)
{
    services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(jwtSettings.GetSecretKeyBytes()),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ValidateAccessTokenAsync
            };
        });
}

static void UseDevelopmentSwagger(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FurniSpace API v1");
        options.RoutePrefix = string.Empty;
    });
}

static void UseProductionHttps(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
}
