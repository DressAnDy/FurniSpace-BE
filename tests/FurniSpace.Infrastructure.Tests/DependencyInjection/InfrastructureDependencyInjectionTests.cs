#nullable enable

using System;
using System.Collections.Generic;
using FurniSpace.Infrastructure;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Data.Mongo;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.DependencyInjection;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersCoreRepositoriesAndMongoProviders()
    {
        var services = new ServiceCollection();
        var configuration = CreateValidConfiguration();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatMessageRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProposalRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMongoDatabaseProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRoomPlannerSceneCollection));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRoomPlannerSceneRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISearchIndexService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IIndexManager));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICacheService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IEmailService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IFileStorageService));
    }

    [Fact]
    public void AddInfrastructure_WhenInitializeIndicesDisabled_DoesNotRegisterElasticsearchInitializer()
    {
        var services = new ServiceCollection();
        var configuration = CreateValidConfiguration(new Dictionary<string, string?>
        {
            ["Elasticsearch:InitializeIndices"] = "false"
        });

        services.AddInfrastructure(configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ImplementationType == typeof(ElasticsearchIndexInitializer));
    }

    [Fact]
    public void AddInfrastructure_WhenInitializeIndicesEnabled_RegistersElasticsearchInitializer()
    {
        var services = new ServiceCollection();
        var configuration = CreateValidConfiguration();

        services.AddInfrastructure(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(ElasticsearchIndexInitializer));
    }

    [Fact]
    public void AddInfrastructure_WhenPostgresConnectionMissing_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Elasticsearch:Url"] = "http://localhost:9200"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("PostgreSQL connection string is missing", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenRedisConnectionMissing_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Elasticsearch:Url"] = "http://localhost:9200"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("Redis connection string is missing", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenElasticsearchUrlMissing_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("Elasticsearch URL is missing", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_PrefersMigrationConnectionAndAlternateEnvKeys()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MigrationConnection"] = "Host=localhost;Database=migration;Username=test;Password=test",
                ["REDIS_CONNECTION"] = "localhost:6380",
                ["ELASTICSEARCH_URL"] = "http://localhost:9201",
                ["ELASTICSEARCH_INDEX_PREFIX"] = "furnispace-test",
                ["MONGODB_CONNECTION_STRING"] = "mongodb://localhost:27017",
                ["MONGODB_DATABASE_NAME"] = "furnispace_test",
                ["FIREBASE_STORAGE_BUCKET"] = "furnispace-test-bucket"
            })
            .Build();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWork));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISearchIndexService));
    }

    private static IConfiguration CreateValidConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Elasticsearch:Url"] = "http://localhost:9200",
            ["Elasticsearch:IndexPrefix"] = "furnispace",
            ["GmailApi:BaseUrl"] = "https://gmail.googleapis.com/",
            ["GmailApi:TimeoutSeconds"] = "15"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
