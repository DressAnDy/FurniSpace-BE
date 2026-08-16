using System.Reflection;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Application.Common.Identity;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Common.Quotations;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.BusinessTypes;
using FurniSpace.Application.Interfaces.Categories;
using FurniSpace.Application.Interfaces.Catalog;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using FurniSpace.Application.Interfaces.Dashboard;
using FurniSpace.Application.Interfaces.Financial;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using FurniSpace.Application.Interfaces.Production;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Application.Interfaces.Quotations;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectAreas;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Accounts;
using FurniSpace.Application.Services.BusinessTypes;
using FurniSpace.Application.Services.Search;
using FurniSpace.Application.Services.Categories;
using FurniSpace.Application.Services.Catalog;
using FurniSpace.Application.Services.CustomizationRequests;
using FurniSpace.Application.Services.Dashboard;
using FurniSpace.Application.Services.Financial;
using FurniSpace.Application.Services.Identity;
using FurniSpace.Application.Services.Notifications;
using FurniSpace.Application.Services.Products;
using FurniSpace.Application.Services.ProductVersions;
using FurniSpace.Application.Services.Production;
using FurniSpace.Application.Services.Proposals;
using FurniSpace.Application.Services.Quotations;
using FurniSpace.Application.Services.ProjectFiles;
using FurniSpace.Application.Services.ProjectChats;
using FurniSpace.Application.Services.ProjectChatMessages;
using FurniSpace.Application.Services.ProjectAreas;
using FurniSpace.Application.Services.ProjectSchedules;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Application.Services.Payments;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Application.Services.Reports;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using PaymentRepository = FurniSpace.Infrastructure.Repositories.IRepository.IPaymentRepository;

namespace FurniSpace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<ProjectWorkflowSettings>(configuration.GetSection(ProjectWorkflowSettings.SectionName));
        services.Configure<OrderWorkflowSettings>(configuration.GetSection(OrderWorkflowSettings.SectionName));
        services.PostConfigure<OrderWorkflowSettings>(settings =>
        {
            if (int.TryParse(configuration["ORDER_DEPOSIT_PERCENT"], out var depositPercent))
            {
                settings.DepositPercent = depositPercent;
            }
        });
        services.PostConfigure<ProjectWorkflowSettings>(settings =>
        {
            if (decimal.TryParse(configuration["PROJECT_START_FEE_AMOUNT"], out var projectStartFeeAmount))
            {
                settings.DefaultProjectStartFeeAmount = projectStartFeeAmount;
            }
        });
        services.Configure<SePayOptions>(configuration.GetSection(SePayOptions.SectionName));
        services.PostConfigure<SePayOptions>(options => SePayOptionsConfiguration.ApplyEnvironmentOverrides(options, configuration));
        services.Configure<PayOsOptions>(configuration.GetSection(PayOsOptions.SectionName));
        services.PostConfigure<PayOsOptions>(options => PayOsOptionsConfiguration.ApplyEnvironmentOverrides(options, configuration));
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
        services.AddScoped<IAdminReportService, AdminReportService>();
        services.AddScoped<IBusinessTypeService, BusinessTypeService>();
        services.AddScoped<IAdminFinancialService, AdminFinancialService>();
        services.AddScoped<IDashboardQueueService, DashboardQueueService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductPreviewImageService, ProductPreviewImageService>();
        services.AddScoped<IProductVersionService, ProductVersionService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IProjectCatalogService, ProjectCatalogService>();
        services.AddScoped<ProductionRequestServiceDependencies>(sp =>
        {
            return new ProductionRequestServiceDependencies(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetService<INotificationDispatcher>(),
                sp.GetService<ILogger<ProductionRequestService>>());
        });
        services.AddScoped<IProductionRequestService, ProductionRequestService>();
        services.AddScoped<ProposalServiceDependencies>(sp =>
        {
            return new ProposalServiceDependencies(
                sp.GetService<IRoomPlannerSceneRepository>(),
                sp.GetService<INotificationDispatcher>(),
                sp.GetService<ILogger<ProposalService>>(),
                sp.GetService<FurniSpace.Infrastructure.Repositories.IRepository.ICustomizationRequestRepository>(),
                sp.GetService<IQuotationService>());
        });
        services.AddScoped<IProposalService, ProposalService>();
        services.AddScoped<QuotationRecalculationService>();
        services.AddScoped<QuotationServiceDependencies>(sp =>
        {
            return new QuotationServiceDependencies(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<IOptions<OrderWorkflowSettings>>().Value,
                sp.GetRequiredService<QuotationRecalculationService>(),
                sp.GetService<INotificationDispatcher>(),
                sp.GetService<ILogger<QuotationService>>());
        });
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<CustomizationRequestServiceDependencies>(sp =>
            new CustomizationRequestServiceDependencies(
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.ICustomizationRequestRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.ICustomizationRequestVersionRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProposalRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProjectRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProductVersionRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProjectFileRepository>(),
                sp.GetRequiredService<INotificationDispatcher>(),
                sp.GetRequiredService<IUnitOfWork>()));
        services.AddScoped<ICustomizationRequestService, CustomizationRequestService>();
        services.AddScoped<IFileUploadValidator, FileUploadValidator>();
        services.AddScoped<ProjectChatFileUploadDependencies>(sp =>
        {
            var firebaseSettings = sp.GetRequiredService<IOptions<FirebaseStorageSettings>>().Value;
            return new ProjectChatFileUploadDependencies(
                sp.GetRequiredService<IFileStorageService>(),
                sp.GetRequiredService<IFileUploadValidator>(),
                firebaseSettings);
        });
        services.AddScoped<ProjectChatMessageServiceDependencies>(sp =>
        {
            return new ProjectChatMessageServiceDependencies(
                sp.GetRequiredService<IProjectChatRealtimeService>(),
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<ProjectChatFileUploadDependencies>(),
                sp.GetRequiredService<ILogger<ProjectChatMessageServiceDependencies>>(),
                sp.GetService<ISearchIndexService>(),
                sp.GetService<IChatMessageSearchIndexer>(),
                sp.GetService<INotificationDispatcher>());
        });
        services.AddScoped<ProductServiceDependencies>(sp =>
        {
            return new ProductServiceDependencies(
                sp.GetRequiredService<IFileStorageService>(),
                sp.GetRequiredService<ISearchIndexService>(),
                sp.GetRequiredService<IProductSearchIndexer>(),
                sp.GetRequiredService<IOptions<FileUploadSettings>>().Value,
                sp.GetRequiredService<IOptions<ProductPreviewImageSettings>>().Value,
                sp.GetRequiredService<IOptions<FirebaseStorageSettings>>().Value,
                sp.GetService<ILogger<ProductService>>());
        });
        services.AddScoped<ProjectFileServiceDependencies>(sp =>
        {
            return new ProjectFileServiceDependencies(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<IFileStorageService>(),
                sp.GetRequiredService<IOptions<FileUploadSettings>>().Value,
                sp.GetRequiredService<IOptions<FirebaseStorageSettings>>().Value,
                sp.GetService<ISearchIndexService>(),
                sp.GetService<IProjectFileSearchIndexer>());
        });
        services.AddScoped<ProductVersionFileUploadDependencies>(sp =>
        {
            return new ProductVersionFileUploadDependencies(
                sp.GetRequiredService<IFileStorageService>(),
                sp.GetRequiredService<IOptions<FileUploadSettings>>().Value,
                sp.GetRequiredService<IOptions<ProductPreviewImageSettings>>().Value,
                sp.GetRequiredService<IOptions<FirebaseStorageSettings>>().Value);
        });
        services.AddScoped<IProjectFileService, ProjectFileService>();
        services.AddScoped<IProjectChatService, ProjectChatService>();
        services.AddScoped<IProjectChatMessageService, ProjectChatMessageService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectWorkflowService, ProjectWorkflowService>();
        services.AddScoped<IProjectStakeholderResolver, ProjectStakeholderResolver>();
        services.AddScoped<ProjectStatusTransitionEvaluator>();
        services.AddScoped<ProjectScheduleServiceDependencies>(sp =>
        {
            return new ProjectScheduleServiceDependencies(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<INotificationDispatcher>(),
                sp.GetRequiredService<IOptions<ProjectWorkflowSettings>>().Value);
        });
        services.AddScoped<ProjectServiceDependencies>(sp =>
        {
            return new ProjectServiceDependencies(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<ProjectStatusTransitionEvaluator>(),
                sp.GetService<INotificationDispatcher>(),
                sp.GetService<ILogger<ProjectService>>(),
                sp.GetService<IProjectChatService>(),
                sp.GetService<ISearchIndexService>(),
                sp.GetService<IProjectSearchIndexer>(),
                sp.GetRequiredService<PaymentRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IOrderRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IQuotationRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProposalRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProductionRequestRepository>(),
                sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProjectScheduleRepository>());
        });
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped(static sp => new IdentityVerificationStores(
            sp.GetRequiredService<IPasswordResetStore>(),
            sp.GetRequiredService<IEmailOtpStore>(),
            sp.GetRequiredService<IRefreshTokenStore>()));
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
        services.AddScoped<IPasswordResetStore, PasswordResetStore>();
        services.AddScoped<IEmailOtpStore, EmailOtpStore>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IProjectScheduleService, ProjectScheduleService>();
        services.AddScoped<IProjectAreaService, ProjectAreaService>();
        services.AddScoped<IRoomPlannerSceneRepository, RoomPlannerSceneRepositoryAdapter>();
        services.AddScoped<IRoomPlannerSceneService, RoomPlannerSceneService>();
        services.AddScoped<ISearchReindexService, SearchReindexService>();
        services.AddScoped<IProductSearchIndexer, ProductSearchIndexer>();
        services.AddScoped<IProjectSearchIndexer, ProjectSearchIndexer>();
        services.AddScoped<IChatMessageSearchIndexer, ChatMessageSearchIndexer>();
        services.AddScoped<IProjectFileSearchIndexer, ProjectFileSearchIndexer>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<PaymentServiceDependencies>(sp =>
        {
            return new PaymentServiceDependencies(
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<IOptions<SePayOptions>>().Value,
                sp.GetRequiredService<IOptions<PayOsOptions>>().Value,
                sp.GetRequiredService<IOptions<ProjectWorkflowSettings>>().Value,
                sp.GetRequiredService<SePayVietQrUrlBuilder>(),
                sp.GetRequiredService<IPayOsClient>());
        });
        services.AddScoped<IPaymentBusinessEffectService, PaymentBusinessEffectService>();
        services.AddScoped<IOrderService>(sp => new OrderService(
            sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IOrderRepository>(),
            sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProjectRepository>(),
            sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IPaymentRepository>(),
            sp.GetRequiredService<FurniSpace.Infrastructure.Repositories.IRepository.IProjectScheduleRepository>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetService<INotificationDispatcher>(),
            sp.GetService<ILogger<OrderService>>()));
        services.AddScoped<PaymentWebhookRuntime>();
        services.AddScoped<ISePayWebhookService, SePayWebhookHandler>();
        services.AddScoped<IPayOsWebhookService, PayOsWebhookHandler>();
        services.AddScoped<IPayOsClient, PayOsClientService>();
        services.AddSingleton<SePayVietQrUrlBuilder>();
        services.AddSingleton<SePayWebhookSignatureVerifier>();

        return services;
    }
}
