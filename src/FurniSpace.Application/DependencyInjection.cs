using System.Reflection;
using FurniSpace.Application.Interfaces;
using FurniSpace.Application.Services;
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
        services.AddInfrastructure(configuration);
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
