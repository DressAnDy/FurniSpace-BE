using System.Reflection;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.Categories;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Services.Accounts;
using FurniSpace.Application.Services.Categories;
using FurniSpace.Application.Services.Identity;
using FurniSpace.Application.Services.Products;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Services.ProjectFiles;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure;
using Mapster;
using Microsoft.AspNetCore.Identity;
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
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductVersionService, ProductVersionService>();
        services.AddScoped<IProjectFileService, ProjectFileService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
        services.AddScoped<IPasswordResetStore, PasswordResetStore>();
        services.AddScoped<IEmailOtpStore, EmailOtpStore>();

        return services;
    }
}
