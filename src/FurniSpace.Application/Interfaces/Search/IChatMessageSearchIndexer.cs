namespace FurniSpace.Application.Interfaces.Search;

public interface IChatMessageSearchIndexer
{
    Task SyncMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}
