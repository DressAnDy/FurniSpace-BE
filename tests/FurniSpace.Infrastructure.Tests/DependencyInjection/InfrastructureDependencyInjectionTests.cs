#nullable enable

using System.Collections.Generic;
using FurniSpace.Infrastructure;
using FurniSpace.Infrastructure.Data.Mongo;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.DependencyInjection;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersProjectChatRepositories()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Elasticsearch:Url"] = "http://localhost:9200"
            })
            .Build();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatMessageRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProposalRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMongoDatabaseProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRoomPlannerSceneCollection));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRoomPlannerSceneRepository));
    }
}
