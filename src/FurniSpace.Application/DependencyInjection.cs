using System.Reflection;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.Categories;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Application.Services.Accounts;
using FurniSpace.Application.Services.Categories;
using FurniSpace.Application.Services.Identity;
using FurniSpace.Application.Services.Notifications;
using FurniSpace.Application.Services.Products;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Services.Proposals;
using FurniSpace.Application.Services.ProjectFiles;
using FurniSpace.Application.Services.ProjectChats;
using FurniSpace.Application.Services.ProjectChatMessages;
using FurniSpace.Application.Services.ProjectSchedules;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<ProjectWorkflowSettings>(configuration.GetSection(ProjectWorkflowSettings.SectionName));
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
        services.AddScoped<IProductPreviewImageService, ProductPreviewImageService>();
        services.AddScoped<IProductVersionService, ProductVersionService>();
        services.AddScoped<IProposalService, ProposalService>();
        services.AddScoped<IFileUploadValidator, FileUploadValidator>();
        services.AddScoped<ProjectChatFileUploadDependencies>(sp =>
        {
            var firebaseSettings = sp.GetRequiredService<IOptions<FirebaseStorageSettings>>().Value;
            return new ProjectChatFileUploadDependencies(
                sp.GetRequiredService<IFileStorageService>(),
                sp.GetRequiredService<IFileUploadValidator>(),
                firebaseSettings);
        });
        services.AddScoped<IProjectFileService, ProjectFileService>();
        services.AddScoped<IProjectChatService, ProjectChatService>();
        services.AddScoped<IProjectChatMessageService, ProjectChatMessageService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ProjectStatusTransitionEvaluator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
        services.AddScoped<IPasswordResetStore, PasswordResetStore>();
        services.AddScoped<IEmailOtpStore, EmailOtpStore>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IProjectScheduleService, ProjectScheduleService>();
        services.AddScoped<IRoomPlannerSceneRepository, RoomPlannerSceneRepositoryAdapter>();
        services.AddScoped<IRoomPlannerSceneService, RoomPlannerSceneService>();

        return services;
    }
}
