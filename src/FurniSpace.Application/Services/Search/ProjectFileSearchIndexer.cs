using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public sealed class ProjectFileSearchIndexer : IProjectFileSearchIndexer
{
    private const string ProjectFileIndexName = "project-files";

    private readonly IProjectFileRepository _files;
    private readonly ISearchIndexService _search;

    public ProjectFileSearchIndexer(
        IProjectFileRepository files,
        ISearchIndexService search)
    {
        _files = files;
        _search = search;
    }

    public async Task SyncFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _files.GetSearchIndexItemAsync(fileId, cancellationToken);
            if (item is null || !ProjectFileSearchDocumentMapper.IsIndexable(item))
            {
                await _search.DeleteAsync(ProjectFileIndexName, fileId.ToString(), cancellationToken);
                return;
            }

            var document = ProjectFileSearchDocumentMapper.ToDocument(item);
            await _search.IndexAsync(ProjectFileIndexName, fileId.ToString(), document, cancellationToken);
        }
        catch
        {
            // Search indexing is eventually consistent and should not fail the database write.
        }
    }
}
