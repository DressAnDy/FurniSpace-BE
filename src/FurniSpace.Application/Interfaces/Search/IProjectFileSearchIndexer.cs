namespace FurniSpace.Application.Interfaces.Search;

public interface IProjectFileSearchIndexer
{
    Task SyncFileAsync(Guid fileId, CancellationToken cancellationToken = default);
}
