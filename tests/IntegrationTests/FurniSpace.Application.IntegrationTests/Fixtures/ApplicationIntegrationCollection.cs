using FurniSpace.Testing.Fixtures;

namespace FurniSpace.Application.IntegrationTests.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApplicationIntegrationCollection : ICollectionFixture<ApplicationIntegrationFixture>
{
    public const string Name = "Application PostgreSQL integration";
}

public sealed class ApplicationIntegrationFixture : IAsyncLifetime
{
    public PostgresIntegrationDatabase Database { get; } = new();

    public Task InitializeAsync() => Database.StartAsync();

    public Task DisposeAsync() => Database.DisposeAsync().AsTask();
}
