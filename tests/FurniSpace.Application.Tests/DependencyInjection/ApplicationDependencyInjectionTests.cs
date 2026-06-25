#nullable enable

using System.Collections.Generic;
using FurniSpace.Application;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FurniSpace.Application.Tests.DependencyInjection;

public sealed class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersProjectChatServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["JwtSettings:SecretKey"] = "01234567890123456789012345678901",
                ["JwtSettings:Issuer"] = "test",
                ["JwtSettings:Audience"] = "test",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Elasticsearch:Url"] = "http://localhost:9200"
            })
            .Build();

        services.AddApplication(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatMessageService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProposalService));
    }
}
