using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Common.Search;

namespace FurniSpace.Application.Services.Search;

public sealed class ProjectSearchIndexer : IProjectSearchIndexer
{
    private const string ProjectIndexName = "projects";

    private readonly IProjectRepository _projects;
    private readonly ISearchIndexService _search;

    public ProjectSearchIndexer(
        IProjectRepository projects,
        ISearchIndexService search)
    {
        _projects = projects;
        _search = search;
    }

    public async Task SyncProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _projects.GetSearchIndexItemAsync(projectId, cancellationToken);
            if (item is null)
            {
                await _search.DeleteAsync(ProjectIndexName, projectId.ToString(), cancellationToken);
                return;
            }

            var document = ProjectSearchDocumentMapper.ToDocument(item);
            await _search.IndexAsync(ProjectIndexName, projectId.ToString(), document, cancellationToken);
        }
        catch
        {
            // Search indexing is eventually consistent and should not fail the database write.
        }
    }
}
