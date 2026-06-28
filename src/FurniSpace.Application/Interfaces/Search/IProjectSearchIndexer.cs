namespace FurniSpace.Application.Interfaces.Search;

public interface IProjectSearchIndexer
{
    Task SyncProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
