using FurniSpace.Testing.Fixtures;

namespace FurniSpace.Infrastructure.IntegrationTests.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresIntegrationFixture>
{
    public const string Name = "PostgreSQL integration";
}

public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    public PostgresIntegrationDatabase Database { get; } = new();

    public Task InitializeAsync() => Database.StartAsync();

    public Task DisposeAsync() => Database.DisposeAsync().AsTask();
}
