using FurniSpace.Application.Interfaces.Search;

namespace FurniSpace.Testing.Fakes;

public sealed class NoOpSearchIndexer :
    IProductSearchIndexer,
    IProjectSearchIndexer,
    IChatMessageSearchIndexer,
    IProjectFileSearchIndexer
{
    public Task SyncProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SyncProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SyncMessageAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SyncFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
