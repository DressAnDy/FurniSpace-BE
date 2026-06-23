using Elastic.Clients.Elasticsearch;
using FurniSpace.Infrastructure.Caching;
using FurniSpace.Infrastructure.Common.Caching;
using FurniSpace.Infrastructure.Common.Email;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Email;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Repositories.Repository;
using FurniSpace.Infrastructure.Search;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
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
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductVersionRepository, ProductVersionRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
        services.AddScoped<IProjectChatRepository, ProjectChatRepository>();
        services.AddScoped<IProjectChatMessageRepository, ProjectChatMessageRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IProjectScheduleRepository, ProjectScheduleRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IFileStorageService, FirebaseStorageService>();

        return services;
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

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        MapPostgresEnums(dataSourceBuilder);
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
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
        builder.MapEnum<ProjectScheduleType>("project_schedule_type", translator);
        builder.MapEnum<ProjectScheduleStatus>("project_schedule_status", translator);
        builder.MapEnum<ProposalStatus>("proposal_status", translator);
        builder.MapEnum<ProposalSceneType>("proposal_scene_type", translator);
        builder.MapEnum<ProposalSceneVariantStatus>("proposal_scene_variant_status", translator);
        builder.MapEnum<ProposalSceneVariantType>("proposal_scene_variant_type", translator);
        builder.MapEnum<CustomizationStatus>("customization_status", translator);
        builder.MapEnum<QuotationStatus>("quotation_status", translator);
        builder.MapEnum<OrderStatus>("order_status", translator);
        builder.MapEnum<OrderItemStatus>("order_item_status", translator);
        builder.MapEnum<PaymentStatus>("payment_status", translator);
        builder.MapEnum<PaymentType>("payment_type", translator);
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
    }

    private static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
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

        return services;
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
