#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        services.AddApplication(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProjectChatMessageService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProposalService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrderService));
    }

    [Fact]
    public void AddApplication_RegistersProjectFileServiceDependenciesWithDirectUploadStorage()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["JwtSettings:SecretKey"] = "01234567890123456789012345678901",
                ["JwtSettings:Issuer"] = "test",
                ["JwtSettings:Audience"] = "test",
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        services.AddApplication(configuration);
        services.AddSingleton<IUnitOfWork, NoOpUnitOfWork>();
        services.AddSingleton<IFileStorageService, NoOpFileStorageService>();
        services.AddSingleton<IDirectFileUploadStorageService, NoOpDirectUploadStorageService>();
        services.AddOptions<FirebaseStorageSettings>().Configure(settings => settings.Bucket = "test-bucket");
        services.AddOptions<FileUploadSettings>().Configure(settings => settings.MaxFileSizeBytes = 1024 * 1024);

        var provider = services.BuildServiceProvider();
        var dependencies = provider.GetRequiredService<ProjectFileServiceDependencies>();

        Assert.NotNull(dependencies.DirectUploadStorage);
        Assert.Equal("test-bucket", dependencies.FirebaseSettings.Bucket);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpFileStorageService : IFileStorageService
    {
        public Task<StorageUploadResult> UploadAsync(StorageUploadRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageUploadResult());

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpDirectUploadStorageService : IDirectFileUploadStorageService
    {
        public Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
            StorageSignedUploadRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSignedUploadResult());

        public Task<StorageUploadResult> FinalizeDirectUploadAsync(
            StorageDirectUploadFinalizeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageUploadResult());
    }
}
