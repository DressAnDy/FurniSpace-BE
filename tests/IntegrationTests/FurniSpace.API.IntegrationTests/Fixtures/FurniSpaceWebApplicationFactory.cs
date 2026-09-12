using FurniSpace.API.IntegrationTests.Authentication;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Testing.Fakes;
using FurniSpace.Testing.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using StackExchange.Redis;

namespace FurniSpace.API.IntegrationTests.Fixtures;

public sealed class FurniSpaceWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgresIntegrationDatabase _database;

    public FurniSpaceWebApplicationFactory(PostgresIntegrationDatabase database)
    {
        _database = database;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _database.ConnectionString,
                ["ConnectionStrings:MigrationConnection"] = _database.ConnectionString,
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
                ["Smtp:Host"] = "127.0.0.1",
                ["Smtp:FromEmail"] = "integration@furnispace.test",
                ["FirebaseStorage:Bucket"] = "furnispace-integration",
                ["PayOS:Enabled"] = "true",
                ["PayOS:ReturnUrl"] = "https://frontend.integration.test/payments/return",
                ["PayOS:CancelUrl"] = "https://frontend.integration.test/payments/cancel",
                ["SePay:Enabled"] = "false",
                ["MongoDb:ConnectionString"] = "mongodb://127.0.0.1:1",
                ["MongoDb:DatabaseName"] = "furnispace_integration",
                ["MONGODB_CONNECTION_STRING"] = "mongodb://127.0.0.1:1",
                ["MONGODB_DATABASE_NAME"] = "furnispace_integration",
                ["JwtSettings:SecretKey"] = ApiIntegrationFixture.TestJwtSecret,
                ["JwtSettings:Issuer"] = "FurniSpace.IntegrationTests",
                ["JwtSettings:Audience"] = "FurniSpace.IntegrationTests",
                ["StartupTasks:RunMigrations"] = "false",
                ["StartupTasks:SeedDemoData"] = "false",
                ["StartupTasks:RunMongoIndexes"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddSingleton(_database.DataSource);
            services.AddDbContext<AppDbContext>((serviceProvider, options) =>
                options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            services.RemoveAll<INotificationDispatcher>();
            services.RemoveAll<IFileStorageService>();
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<ICacheService>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IPayOsClient>();
            services.RemoveAll<IRealtimeNotificationService>();
            services.RemoveAll<IProjectChatRealtimeService>();
            services.RemoveAll<IPaymentRealtimeService>();
            services.AddSingleton<CapturingNotificationDispatcher>();
            services.AddSingleton<INotificationDispatcher>(serviceProvider =>
                serviceProvider.GetRequiredService<CapturingNotificationDispatcher>());
            services.AddScoped<IFileStorageService, FakeFileStorageService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            services.AddSingleton<CapturingEmailService>();
            services.AddSingleton<IEmailService>(serviceProvider =>
                serviceProvider.GetRequiredService<CapturingEmailService>());
            services.AddSingleton<IPayOsClient, FakePayOsClient>();
            services.AddSingleton<IRealtimeNotificationService, NoOpRealtimeNotificationService>();
            services.AddSingleton<IProjectChatRealtimeService, NoOpProjectChatRealtimeService>();
            services.AddSingleton<IPaymentRealtimeService, NoOpPaymentRealtimeService>();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}
