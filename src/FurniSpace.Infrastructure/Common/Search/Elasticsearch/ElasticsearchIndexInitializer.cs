using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Infrastructure.Common.Search;

public sealed class ElasticsearchIndexInitializer : IHostedService
{
    private static readonly string[] ManagedIndices =
        ["accounts", "products", "projects", "chat-messages", "project-files"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ElasticsearchIndexInitializer> _logger;

    public ElasticsearchIndexInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<ElasticsearchIndexInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var indexManager = scope.ServiceProvider.GetRequiredService<IIndexManager>();

        foreach (var indexName in ManagedIndices)
        {
            try
            {
                await indexManager.EnsureIndexAsync(indexName, cancellationToken);
                _logger.LogInformation("Elasticsearch index {IndexName} is ready.", indexName);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to ensure Elasticsearch index {IndexName}. Search may fall back to PostgreSQL.",
                    indexName);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
