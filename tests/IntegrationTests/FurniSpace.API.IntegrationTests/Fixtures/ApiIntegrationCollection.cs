using FurniSpace.Testing.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FurniSpace.API.IntegrationTests.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiIntegrationCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Name = "API PostgreSQL integration";
}

public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    public const string TestJwtSecret = "integration-test-secret-key-32-bytes-minimum";
    private const string PlaceholderRedis = "localhost:6379,abortConnect=false";
    private const string PlaceholderElasticsearch = "http://localhost:9200";

    private readonly List<(string Key, string? Previous)> _environmentOverrides = [];
    private FurniSpaceWebApplicationFactory? _factory;
    private HttpClient? _client;

    public PostgresIntegrationDatabase Database { get; } = new();

    public FurniSpaceWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException("API integration fixture has not been initialized.");

    public HttpClient Client =>
        _client ?? throw new InvalidOperationException("API integration fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        await Database.StartAsync();

        // Program / AddInfrastructure validate these during CreateBuilder / early DI,
        // before factory ConfigureAppConfiguration is reliably applied.
        SetEnvironment("DOTNET_ENVIRONMENT", "IntegrationTest");
        SetEnvironment("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        SetEnvironment("JWT_SECRET", TestJwtSecret);
        SetEnvironment("JwtSettings__SecretKey", TestJwtSecret);
        SetEnvironment("ConnectionStrings__DefaultConnection", Database.ConnectionString);
        SetEnvironment("ConnectionStrings__MigrationConnection", Database.ConnectionString);
        SetEnvironment("Redis__ConnectionString", PlaceholderRedis);
        SetEnvironment("REDIS_CONNECTION", PlaceholderRedis);
        SetEnvironment("Elasticsearch__Url", PlaceholderElasticsearch);
        SetEnvironment("ELASTICSEARCH_URL", PlaceholderElasticsearch);
        SetEnvironment("PAYOS_ENABLED", "true");
        SetEnvironment("PAYOS_RETURN_URL", "https://frontend.integration.test/payments/return");
        SetEnvironment("PAYOS_CANCEL_URL", "https://frontend.integration.test/payments/cancel");

        try
        {
            _factory = new FurniSpaceWebApplicationFactory(Database);
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _client?.Dispose();
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }

            await Database.DisposeAsync();
        }
        finally
        {
            _client = null;
            _factory = null;
            RestoreEnvironment();
        }
    }

    private void SetEnvironment(string key, string value)
    {
        _environmentOverrides.Add((key, Environment.GetEnvironmentVariable(key)));
        Environment.SetEnvironmentVariable(key, value);
    }

    private void RestoreEnvironment()
    {
        for (var index = _environmentOverrides.Count - 1; index >= 0; index--)
        {
            var (key, previous) = _environmentOverrides[index];
            Environment.SetEnvironmentVariable(key, previous);
        }

        _environmentOverrides.Clear();
    }
}
