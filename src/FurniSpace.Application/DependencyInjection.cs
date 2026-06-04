using System.Reflection;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Services.Accounts;
using FurniSpace.Application.Services.Identity;
using FurniSpace.Infrastructure;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FurniSpace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.PostConfigure<JwtSettings>(settings =>
        {
            if (string.IsNullOrWhiteSpace(settings.SecretKey))
            {
                settings.SecretKey = configuration["JWT_SECRET"] ?? string.Empty;
            }

            _ = settings.GetSecretKeyBytes();
        });
        services.AddInfrastructure(configuration);
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        return services;
    }
}
