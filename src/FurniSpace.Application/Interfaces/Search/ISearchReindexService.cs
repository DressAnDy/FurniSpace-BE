namespace FurniSpace.Application.Interfaces.Search;

public interface ISearchReindexService
{
    Task ReindexAccountsAsync(CancellationToken cancellationToken = default);

    Task ReindexProductsAsync(CancellationToken cancellationToken = default);

    Task ReindexProjectsAsync(CancellationToken cancellationToken = default);

    Task ReindexChatMessagesAsync(CancellationToken cancellationToken = default);

    Task ReindexProjectFilesAsync(CancellationToken cancellationToken = default);
}
