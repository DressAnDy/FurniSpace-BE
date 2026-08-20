using Elastic.Clients.Elasticsearch;
using FurniSpace.Infrastructure.Caching;
using FurniSpace.Infrastructure.Common.Caching;
using FurniSpace.Infrastructure.Common.Email;
using FurniSpace.Infrastructure.Common.Mongo;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Data.Mongo;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Repositories.Repository;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.NameTranslation;
using StackExchange.Redis;

namespace FurniSpace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<ElasticsearchSettings>(configuration.GetSection(ElasticsearchSettings.SectionName));
        services.Configure<GmailApiSettings>(configuration.GetSection(GmailApiSettings.SectionName));
        services.Configure<MongoDbSettings>(settings =>
        {
            var section = configuration.GetSection(MongoDbSettings.SectionName);
            settings.ConnectionString = configuration["MONGODB_CONNECTION_STRING"]
                ?? section["ConnectionString"]
                ?? settings.ConnectionString;
            settings.DatabaseName = configuration["MONGODB_DATABASE_NAME"]
                ?? section["DatabaseName"]
                ?? settings.DatabaseName;
            settings.RoomPlannerScenesCollectionName = configuration["MONGODB_ROOM_PLANNER_SCENES_COLLECTION"]
                ?? section["RoomPlannerScenesCollectionName"]
                ?? settings.RoomPlannerScenesCollectionName;
        });
        services.Configure<FileUploadSettings>(configuration.GetSection(FileUploadSettings.SectionName));
        services.Configure<ProductPreviewImageSettings>(configuration.GetSection(ProductPreviewImageSettings.SectionName));
        services.Configure<FirebaseStorageSettings>(settings =>
        {
            var section = configuration.GetSection(FirebaseStorageSettings.SectionName);
            settings.Bucket = configuration["FIREBASE_STORAGE_BUCKET"]
                ?? section["Bucket"]
                ?? settings.Bucket;
            settings.CredentialsPath = configuration["FIREBASE_CREDENTIALS_PATH"]
                ?? configuration["GOOGLE_APPLICATION_CREDENTIALS"]
                ?? section["CredentialsPath"]
                ?? settings.CredentialsPath;
            settings.ProjectFilesPrefix = section["ProjectFilesPrefix"] ?? settings.ProjectFilesPrefix;
            if (long.TryParse(section["MaxFileSizeBytes"], out var maxFileSizeBytes))
            {
                settings.MaxFileSizeBytes = maxFileSizeBytes;
            }
        });

        services.AddPostgres(configuration);
        services.AddRedis(configuration);
        services.AddElasticsearch(configuration);
        services.AddMongoRoomPlanner();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAdminReportRepository, AdminReportRepository>();
        services.AddScoped<IBusinessTypeRepository, BusinessTypeRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IProductVersionRepository, ProductVersionRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
        services.AddScoped<IProjectChatRepository, ProjectChatRepository>();
        services.AddScoped<IProjectChatMessageRepository, ProjectChatMessageRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectWorkflowRepository, ProjectWorkflowRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IProjectScheduleRepository, ProjectScheduleRepository>();
        services.AddScoped<IProjectAreaRepository, ProjectAreaRepository>();
        services.AddScoped<IProposalRepository, ProposalRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductionRequestRepository, ProductionRequestRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IFinancialReadRepository, FinancialReadRepository>();
        services.AddScoped<IDashboardQueueReadRepository, DashboardQueueReadRepository>();
        services.AddScoped<ICustomizationRequestRepository, CustomizationRequestRepository>();
        services.AddScoped<ICustomizationRequestVersionRepository, CustomizationRequestVersionRepository>();
        services.AddScoped<IRoomPlannerSceneRepository, RoomPlannerSceneRepository>();
        services.AddScoped<IRoomPlannerProposalSceneRepository, RoomPlannerProposalSceneRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHttpClient(GmailEmailClientNames.OAuth, (serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<GmailApiSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 60));
        });
        services.AddSingleton<IGmailAccessTokenProvider, GmailAccessTokenProvider>();
        services.AddHttpClient<IEmailService, GmailApiEmailService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<GmailApiSettings>>().Value;
            var baseUrl = settings.BaseUrl.EndsWith("/", StringComparison.Ordinal)
                ? settings.BaseUrl
                : $"{settings.BaseUrl}/";

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 60));
        });
        services.AddScoped<IFileStorageService, FirebaseStorageService>();

        return services;
    }

    private static void AddMongoRoomPlanner(this IServiceCollection services)
    {
        services.AddSingleton<IMongoDatabaseProvider, MongoDatabaseProvider>();
        services.AddScoped<IRoomPlannerSceneCollection, MongoRoomPlannerSceneCollection>();
    }

    private static void AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MigrationConnection")
            ?? configuration["ConnectionStrings__MigrationConnection"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string is missing. Set ConnectionStrings__DefaultConnection.");
        }

        services.AddSingleton(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            MapPostgresEnums(dataSourceBuilder);
            return dataSourceBuilder.Build();
        });
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
    }

    private static void MapPostgresEnums(NpgsqlDataSourceBuilder builder)
    {
        var translator = new NpgsqlNullNameTranslator();

        builder.MapEnum<AccountStatus>("account_status", translator);
        builder.MapEnum<ProjectStatus>("project_status", translator);
        builder.MapEnum<ProjectAreaType>("project_area_type", translator);
        builder.MapEnum<ProjectAreaStatus>("project_area_status", translator);
        builder.MapEnum<ProjectPhaseType>("project_phase_type", translator);
        builder.MapEnum<ProjectScheduleType>("project_schedule_type", translator);
        builder.MapEnum<ProjectScheduleStatus>("project_schedule_status", translator);
        builder.MapEnum<ProposalStatus>("proposal_status", translator);
        builder.MapEnum<ProposalSceneType>("proposal_scene_type", translator);
        builder.MapEnum<ProposalSceneVariantStatus>("proposal_scene_variant_status", translator);
        builder.MapEnum<ProposalSceneVariantType>("proposal_scene_variant_type", translator);
        builder.MapEnum<CustomizationStatus>("customization_status", translator);
        builder.MapEnum<CustomizationVersionStatus>("customization_version_status", translator);
        builder.MapEnum<ProductionFeasibilityStatus>("production_feasibility_status", translator);
        builder.MapEnum<QuotationStatus>("quotation_status", translator);
        builder.MapEnum<QuotationItemType>("quotation_item_type", translator);
        builder.MapEnum<OrderStatus>("order_status", translator);
        builder.MapEnum<OrderItemStatus>("order_item_status", translator);
        builder.MapEnum<PaymentStatus>("payment_status", translator);
        builder.MapEnum<PaymentType>("payment_type", translator);
        builder.MapEnum<PaymentProvider>("payment_provider", translator);
        builder.MapEnum<PaymentMethod>("payment_method", translator);
        builder.MapEnum<PaymentTransactionType>("payment_transaction_type", translator);
        builder.MapEnum<PaymentTransactionStatus>("payment_transaction_status", translator);
        builder.MapEnum<ProductionRequestStatus>("production_request_status", translator);
        builder.MapEnum<ProductionItemStatus>("production_item_status", translator);

        builder.MapEnum<ProjectChatType>("project_chat_type", translator);
        builder.MapEnum<ProjectChatStatus>("project_chat_status", translator);
        builder.MapEnum<ProjectChatMessageType>("project_chat_message_type", translator);
        builder.MapEnum<FileStatus>("file_status", translator);
        builder.MapEnum<FileVisibility>("file_visibility", translator);
        builder.MapEnum<FileType>("file_type", translator);
        builder.MapEnum<ProductStatus>("product_status", translator);
        builder.MapEnum<ProductVersionType>("product_version_type", translator);
        builder.MapEnum<LayoutAssetType>("layout_asset_type", translator);
        builder.MapEnum<LayoutAssetStatus>("layout_asset_status", translator);
    }

    private static void AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetSection(RedisSettings.SectionName)["ConnectionString"]
            ?? configuration["REDIS_CONNECTION"];

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            throw new InvalidOperationException("Redis connection string is missing. Set Redis__ConnectionString or REDIS_CONNECTION.");
        }

        redisConnection = AppendRedisPasswordIfNeeded(redisConnection);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<ICacheService, RedisCacheService>();
    }

    private static void AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        var url = configuration.GetSection(ElasticsearchSettings.SectionName)["Url"]
            ?? configuration["ELASTICSEARCH_URL"];

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "Elasticsearch URL is missing. Set Elasticsearch__Url or ELASTICSEARCH_URL.");
        }

        var indexPrefix = configuration.GetSection(ElasticsearchSettings.SectionName)["IndexPrefix"]
            ?? configuration["ELASTICSEARCH_INDEX_PREFIX"]
            ?? "furnispace";

        var settings = new ElasticsearchClientSettings(new Uri(url))
            .DefaultIndex(indexPrefix);

        services.AddSingleton(new ElasticsearchClient(settings));
        services.AddScoped<ISearchIndexService, ElasticsearchIndexService>();
        services.AddScoped<IIndexManager, ElasticsearchIndexManager>();
        if (configuration.GetValue("Elasticsearch:InitializeIndices", true))
        {
            services.AddHostedService<ElasticsearchIndexInitializer>();
        }
    }

    private static string AppendRedisPasswordIfNeeded(string connectionString)
    {
        if (connectionString.Contains("password=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        if (string.IsNullOrWhiteSpace(redisPassword))
        {
            return connectionString;
        }

        return $"{connectionString},password={redisPassword},abortConnect=false";
    }
}
