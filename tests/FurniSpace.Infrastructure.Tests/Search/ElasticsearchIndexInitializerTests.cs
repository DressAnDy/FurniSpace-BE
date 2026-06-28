#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Search;

public sealed class ElasticsearchIndexInitializerTests
{
    [Fact]
    public async Task StartAsync_EnsuresAllManagedIndices()
    {
        var manager = new CapturingIndexManager();
        var initializer = new ElasticsearchIndexInitializer(
            new TestScopeFactory(manager),
            NullLogger<ElasticsearchIndexInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None);

        Assert.Equal(
            ["accounts", "products", "projects", "chat-messages", "project-files"],
            manager.EnsuredIndices);
    }

    [Fact]
    public async Task StartAsync_WhenEnsureThrows_ContinuesWithRemainingIndices()
    {
        var manager = new CapturingIndexManager(throwForIndex: "products");
        var initializer = new ElasticsearchIndexInitializer(
            new TestScopeFactory(manager),
            NullLogger<ElasticsearchIndexInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None);

        Assert.Equal(5, manager.EnsuredIndices.Count);
        Assert.Contains("project-files", manager.EnsuredIndices);
    }

    [Fact]
    public async Task StopAsync_Completes()
    {
        var initializer = new ElasticsearchIndexInitializer(
            new TestScopeFactory(new CapturingIndexManager()),
            NullLogger<ElasticsearchIndexInitializer>.Instance);

        var stopTask = initializer.StopAsync(CancellationToken.None);

        await stopTask;

        Assert.True(stopTask.IsCompletedSuccessfully);
    }

    private sealed class CapturingIndexManager : IIndexManager
    {
        private readonly string? _throwForIndex;

        public CapturingIndexManager(string? throwForIndex = null)
        {
            _throwForIndex = throwForIndex;
        }

        public List<string> EnsuredIndices { get; } = [];

        public Task EnsureIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            EnsuredIndices.Add(indexName);
            if (string.Equals(indexName, _throwForIndex, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Index unavailable.");
            }

            return Task.CompletedTask;
        }

        public Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly IIndexManager _manager;

        public TestScopeFactory(IIndexManager manager)
        {
            _manager = manager;
        }

        public IServiceScope CreateScope()
            => new TestScope(_manager);
    }

    private sealed class TestScope : IServiceScope
    {
        public TestScope(IIndexManager manager)
        {
            ServiceProvider = new TestServiceProvider(manager);
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly IIndexManager _manager;

        public TestServiceProvider(IIndexManager manager)
        {
            _manager = manager;
        }

        public object? GetService(Type serviceType)
            => serviceType == typeof(IIndexManager) ? _manager : null;
    }
}
