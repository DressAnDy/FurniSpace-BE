using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.IntegrationTests.Fixtures;
using FurniSpace.Infrastructure.ReadModels.ProjectChats;
using FurniSpace.Infrastructure.Repositories.Repository;
using FurniSpace.Testing.Seeding;

namespace FurniSpace.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgresIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProjectChatRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgresIntegrationFixture _fixture;

    public ProjectChatRepositoryIntegrationTests(PostgresIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetListAsync_UsesPostgresEnumsAndExcludesArchivedChatsByDefault()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var scenario = await ProjectChatScenarioSeeder.SeedAsync(context);
        var repository = new ProjectChatRepository(context);

        var (items, total) = await repository.GetListAsync(
            scenario.ProjectId,
            new ProjectChatListQueryReadModel());

        Assert.Equal(2, total);
        Assert.Equal(
            [scenario.DesignerChatId, scenario.SalesChatId],
            items.Select(item => item.ChatId).ToArray());
        Assert.DoesNotContain(items, item => item.ChatId == scenario.ArchivedChatId);
    }

    [Fact]
    public async Task GetListAsync_ProjectsLatestMessageThroughPostgresQuery()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var scenario = await ProjectChatScenarioSeeder.SeedAsync(context);
        var repository = new ProjectChatRepository(context);

        var (items, _) = await repository.GetListAsync(
            scenario.ProjectId,
            new ProjectChatListQueryReadModel { ChatType = ProjectChatType.SALES });

        var chat = Assert.Single(items);
        Assert.NotNull(chat.LastMessage);
        Assert.Equal(scenario.LatestMessageId, chat.LastMessage.MessageId);
        Assert.Equal("Sales User", chat.LastMessage.SenderName);
        Assert.Equal(200, chat.LastMessage.ContentPreview?.Length);
    }

    [Fact]
    public async Task GetAccessAsync_ReturnsParticipantAndDatabaseRole()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var scenario = await ProjectChatScenarioSeeder.SeedAsync(context);
        var repository = new ProjectChatRepository(context);

        var access = await repository.GetAccessAsync(
            scenario.ProjectId,
            scenario.SalesAccountId);

        Assert.NotNull(access);
        Assert.Equal(scenario.ProjectId, access.ProjectId);
        Assert.Equal(scenario.CustomerAccountId, access.CustomerId);
        Assert.Equal(scenario.SalesAccountId, access.AssignedSalesId);
        Assert.Equal(scenario.DesignerAccountId, access.AssignedDesignerId);
        Assert.Equal("SALES", access.RoleName);
    }
}
