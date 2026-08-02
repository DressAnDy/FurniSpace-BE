using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FurniSpace.API.Cli;
using FurniSpace.API.Filters;
using FurniSpace.API.Hubs;
using FurniSpace.API.Realtime;
using FurniSpace.Application;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.API.Middleware;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Logging;
using FurniSpace.Shared.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using RoomPlannerSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerSceneRepository;

var bootstrapEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (!string.Equals(bootstrapEnvironment, "IntegrationTest", StringComparison.OrdinalIgnoreCase))
{
    EnvLoader.LoadEnv(required: false);
}

if (TryGetReindexModule(args, out var reindexModule))
{
    await RunReindexCommandAsync(reindexModule);
    return;
}

const string AllowAllCorsPolicy = "AllowAllCors";
const string WildcardCorsOrigin = "*";
const string CorsAllowedOriginsEnvKey = "CORS_ALLOWED_ORIGINS";
const string ReindexCompletedLogTemplate = "Elasticsearch reindex completed for module {Module}.";

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = LoadJwtSettings(builder.Configuration);

Log.Logger = SerilogConfiguration.CreateLogger(
    builder.Configuration,
    useJsonFormatting: !builder.Environment.IsDevelopment());

builder.Host.UseSerilog();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
ConfigureForwardedHeaders(builder.Services, builder.Configuration);
ConfigureAuthCookies(builder.Services);
AddAllowAllCors(builder.Services, builder.Configuration);
AddPublicAuthRateLimiter(builder.Services);
AddApiSwagger(builder.Services);
builder.Services.AddApplication(builder.Configuration);
AddJwtAuthentication(builder.Services, jwtSettings, builder.Environment);
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotificationService, SignalRRealtimeNotificationService>();
builder.Services.AddScoped<IProjectChatRealtimeService, SignalRProjectChatRealtimeService>();
builder.Services.AddScoped<IPaymentRealtimeService, SignalRPaymentRealtimeService>();

var app = builder.Build();

await RunStartupDatabaseTasksAsync(app);
await RunStartupMongoTasksAsync(app);
UseDevelopmentSwagger(app);
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.UseCors(AllowAllCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationsHub>(RealtimeGroupNames.HubPath);
app.MapHub<ProjectChatHub>(ProjectChatRealtimeConstants.HubPath);
app.MapHub<PaymentHub>(PaymentRealtimeConstants.HubPath);
app.MapGet("/", () => "FurniSpace API");
MapRedisDebugHealth(app);
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

static async Task RunStartupDatabaseTasksAsync(WebApplication app)
{
    var runMigrations = app.Configuration.GetValue("StartupTasks:RunMigrations", true);
    var seedDemoData = app.Configuration.GetValue("StartupTasks:SeedDemoData", true);
    if (!runMigrations && !seedDemoData)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (runMigrations)
        {
            await dbContext.Database.MigrateAsync();
        }

        if (seedDemoData)
        {
            await DataSeeder.SeedAsync(dbContext);
        }
    }
    catch (Exception exception)
    {
        Log.Error(exception, "Failed to initialize the database during startup.");
        if (app.Environment.IsEnvironment("IntegrationTest"))
        {
            throw;
        }
    }
}

static async Task RunStartupMongoTasksAsync(WebApplication app)
{
    var runMongoIndexes = app.Configuration.GetValue("StartupTasks:RunMongoIndexes", true);
    if (!runMongoIndexes)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    try
    {
        var roomPlannerScenes = scope.ServiceProvider.GetRequiredService<RoomPlannerSceneRepository>();
        await roomPlannerScenes.EnsureIndexesAsync();
    }
    catch (Exception exception)
    {
        Log.Error(exception, "Failed to initialize MongoDB Room Planner indexes during startup.");
        if (app.Environment.IsEnvironment("IntegrationTest"))
        {
            throw;
        }
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

static void ConfigureAuthCookies(IServiceCollection services)
{
    ConfigureAuthCookie(services, "access_token");
    ConfigureAuthCookie(services, "refresh_token");
}

static void ConfigureAuthCookie(IServiceCollection services, string cookieName)
{
    services.Configure<CookieOptions>(cookieName, options =>
    {
        options.HttpOnly = true;
        options.Secure = true;
        options.SameSite = SameSiteMode.None;
        options.Path = "/";
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
                Array.Empty<string>()
            }
        });
    });
}

static Task ReadJwtBearerTokenAsync(MessageReceivedContext context)
{
    if (string.IsNullOrWhiteSpace(context.Token) &&
        context.Request.Cookies.TryGetValue("access_token", out var accessToken))
    {
        context.Token = accessToken;
    }

    if (string.IsNullOrWhiteSpace(context.Token) && IsRealtimeHubPath(context.HttpContext.Request.Path))
    {
        var queryAccessToken = context.Request.Query["access_token"];
        if (!string.IsNullOrWhiteSpace(queryAccessToken))
        {
            context.Token = queryAccessToken;
        }
    }

    return Task.CompletedTask;
}

static bool IsRealtimeHubPath(PathString path) =>
    path.StartsWithSegments(RealtimeGroupNames.HubPath) ||
    path.StartsWithSegments(ProjectChatRealtimeConstants.HubPath);

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
                OnMessageReceived = ReadJwtBearerTokenAsync,
                OnTokenValidated = ValidateAccessTokenAsync
            };
        });
}

static void UseDevelopmentSwagger(WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FurniSpace API v1");
        options.RoutePrefix = string.Empty;
    });
}

static void MapRedisDebugHealth(WebApplication app)
{
    var enabled = app.Configuration.GetValue<bool>("REDIS_DEBUG_HEALTH")
        || app.Configuration.GetValue<bool>("Redis:DebugHealth");

    if (!enabled)
    {
        return;
    }

    app.MapGet("/health/redis", async (
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        CancellationToken cancellationToken) =>
    {
        var configuredConnection = configuration.GetSection("Redis")["ConnectionString"]
            ?? configuration["REDIS_CONNECTION"]
            ?? "<missing>";

        var database = redis.GetDatabase();
        var key = $"furnispace:health:{Guid.NewGuid():N}";

        try
        {
            var ping = await database.PingAsync();
            await database.StringSetAsync(key, "ok", TimeSpan.FromMinutes(1));
            var value = await database.StringGetAsync(key);

            return Results.Ok(new
            {
                status = "ok",
                redis.IsConnected,
                pingMs = ping.TotalMilliseconds,
                writeReadOk = value == "ok",
                endpoints = redis.GetEndPoints().Select(endpoint => endpoint.ToString()),
                configuredConnection = RedisConnectionMasker.Mask(configuredConnection)
            });
        }
        catch (Exception exception)
        {
            return Results.Problem(
                title: "Redis health check failed.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object>
                {
                    ["redisConnected"] = redis.IsConnected,
                    ["endpoints"] = redis.GetEndPoints().Select(endpoint => endpoint.ToString()),
                    ["configuredConnection"] = RedisConnectionMasker.Mask(configuredConnection),
                    ["exceptionType"] = exception.GetType().FullName
                });
        }
    });
}

static bool TryGetReindexModule(string[] args, out string module)
{
    module = string.Empty;
    if (args.Length >= 2 &&
        args[0].Equals("reindex", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(args[1]))
    {
        module = args[1].Trim().ToLowerInvariant();
        return true;
    }

    return false;
}

static async Task RunReindexCommandAsync(string module)
{
    var builder = WebApplication.CreateBuilder(Array.Empty<string>());

    Log.Logger = SerilogConfiguration.CreateLogger(
        builder.Configuration,
        useJsonFormatting: false);
    builder.Host.UseSerilog();

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddScoped<IRealtimeNotificationService, NoOpRealtimeNotificationService>();
    builder.Services.AddScoped<IProjectChatRealtimeService, NoOpProjectChatRealtimeService>();
    builder.Services.AddScoped<IPaymentRealtimeService, NoOpPaymentRealtimeService>();

    var app = builder.Build();

    using var scope = app.Services.CreateScope();
    var reindexService = scope.ServiceProvider.GetRequiredService<ISearchReindexService>();

    switch (module)
    {
        case "accounts":
            await reindexService.ReindexAccountsAsync();
            Log.Information(ReindexCompletedLogTemplate, module);
            break;
        case "products":
            await reindexService.ReindexProductsAsync();
            Log.Information(ReindexCompletedLogTemplate, module);
            break;
        case "projects":
            await reindexService.ReindexProjectsAsync();
            Log.Information(ReindexCompletedLogTemplate, module);
            break;
        case "chat-messages":
            await reindexService.ReindexChatMessagesAsync();
            Log.Information(ReindexCompletedLogTemplate, module);
            break;
        case "project-files":
            await reindexService.ReindexProjectFilesAsync();
            Log.Information(ReindexCompletedLogTemplate, module);
            break;
        default:
            throw new InvalidOperationException(
                $"Unsupported reindex module '{module}'. Supported modules: accounts, products, projects, chat-messages, project-files.");
    }

    await Log.CloseAndFlushAsync();
}

public partial class Program;
