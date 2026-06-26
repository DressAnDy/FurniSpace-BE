using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.Search;

public sealed class SearchReindexService : ISearchReindexService
{
    private const string AccountIndexName = "accounts";
    private const string ProductIndexName = "products";
    private const string ProjectIndexName = "projects";
    private const string ChatMessageIndexName = "chat-messages";
    private const string ProjectFileIndexName = "project-files";
    private const int BatchSize = 100;

    private readonly IAccountRepository _accounts;
    private readonly IProductRepository _products;
    private readonly IProjectRepository _projects;
    private readonly IProjectChatMessageRepository _chatMessages;
    private readonly IProjectFileRepository _projectFiles;
    private readonly ISearchIndexService _search;
    private readonly IIndexManager _indexManager;

    public SearchReindexService(
        IAccountRepository accounts,
        IProductRepository products,
        IProjectRepository projects,
        IProjectChatMessageRepository chatMessages,
        IProjectFileRepository projectFiles,
        ISearchIndexService search,
        IIndexManager indexManager)
    {
        _accounts = accounts;
        _products = products;
        _projects = projects;
        _chatMessages = chatMessages;
        _projectFiles = projectFiles;
        _search = search;
        _indexManager = indexManager;
    }

    public async Task ReindexAccountsAsync(CancellationToken cancellationToken = default)
    {
        await _indexManager.EnsureIndexAsync(AccountIndexName, cancellationToken);

        var page = 1;
        while (true)
        {
            var accounts = await _accounts.GetPagedAsync(
                page,
                BatchSize,
                search: null,
                status: null,
                includeDeleted: false,
                cancellationToken);

            if (accounts.Count == 0)
            {
                break;
            }

            var items = accounts
                .Adapt<List<AccountDto>>()
                .Select(account => new BulkIndexItem<AccountDto>(account.AccountId.ToString(), account))
                .ToArray();

            await _search.BulkIndexAsync(AccountIndexName, items, cancellationToken);

            if (accounts.Count < BatchSize)
            {
                break;
            }

            page++;
        }
    }

    public async Task ReindexProductsAsync(CancellationToken cancellationToken = default)
    {
        await _indexManager.EnsureIndexAsync(ProductIndexName, cancellationToken);

        var page = 1;
        while (true)
        {
            var products = await _products.GetSearchIndexPageAsync(page, BatchSize, cancellationToken);
            if (products.Count == 0)
            {
                break;
            }

            var items = new List<BulkIndexItem<ProductSearchDocument>>();
            foreach (var product in products)
            {
                if (!ProductSearchDocumentMapper.IsIndexable(product))
                {
                    await _search.DeleteAsync(ProductIndexName, product.ProductId.ToString(), cancellationToken);
                    continue;
                }

                items.Add(new BulkIndexItem<ProductSearchDocument>(
                    product.ProductId.ToString(),
                    ProductSearchDocumentMapper.ToDocument(product)));
            }

            if (items.Count > 0)
            {
                await _search.BulkIndexAsync(ProductIndexName, items, cancellationToken);
            }

            if (products.Count < BatchSize)
            {
                break;
            }

            page++;
        }
    }

    public async Task ReindexProjectsAsync(CancellationToken cancellationToken = default)
    {
        await _indexManager.EnsureIndexAsync(ProjectIndexName, cancellationToken);

        var page = 1;
        while (true)
        {
            var projects = await _projects.GetSearchIndexPageAsync(page, BatchSize, cancellationToken);
            if (projects.Count == 0)
            {
                break;
            }

            var items = projects
                .Select(project => new BulkIndexItem<ProjectSearchDocument>(
                    project.ProjectId.ToString(),
                    ProjectSearchDocumentMapper.ToDocument(project)))
                .ToArray();

            await _search.BulkIndexAsync(ProjectIndexName, items, cancellationToken);

            if (projects.Count < BatchSize)
            {
                break;
            }

            page++;
        }
    }

    public async Task ReindexChatMessagesAsync(CancellationToken cancellationToken = default)
    {
        await _indexManager.EnsureIndexAsync(ChatMessageIndexName, cancellationToken);

        var page = 1;
        while (true)
        {
            var messages = await _chatMessages.GetSearchIndexPageAsync(page, BatchSize, cancellationToken);
            if (messages.Count == 0)
            {
                break;
            }

            var items = new List<BulkIndexItem<ChatMessageSearchDocument>>();
            foreach (var message in messages)
            {
                if (!ChatMessageSearchDocumentMapper.IsIndexable(message))
                {
                    await _search.DeleteAsync(ChatMessageIndexName, message.MessageId.ToString(), cancellationToken);
                    continue;
                }

                items.Add(new BulkIndexItem<ChatMessageSearchDocument>(
                    message.MessageId.ToString(),
                    ChatMessageSearchDocumentMapper.ToDocument(message)));
            }

            if (items.Count > 0)
            {
                await _search.BulkIndexAsync(ChatMessageIndexName, items, cancellationToken);
            }

            if (messages.Count < BatchSize)
            {
                break;
            }

            page++;
        }
    }

    public async Task ReindexProjectFilesAsync(CancellationToken cancellationToken = default)
    {
        await _indexManager.EnsureIndexAsync(ProjectFileIndexName, cancellationToken);

        var page = 1;
        while (true)
        {
            var files = await _projectFiles.GetSearchIndexPageAsync(page, BatchSize, cancellationToken);
            if (files.Count == 0)
            {
                break;
            }

            var items = new List<BulkIndexItem<ProjectFileSearchDocument>>();
            foreach (var file in files)
            {
                if (!ProjectFileSearchDocumentMapper.IsIndexable(file))
                {
                    await _search.DeleteAsync(ProjectFileIndexName, file.FileId.ToString(), cancellationToken);
                    continue;
                }

                items.Add(new BulkIndexItem<ProjectFileSearchDocument>(
                    file.FileId.ToString(),
                    ProjectFileSearchDocumentMapper.ToDocument(file)));
            }

            if (items.Count > 0)
            {
                await _search.BulkIndexAsync(ProjectFileIndexName, items, cancellationToken);
            }

            if (files.Count < BatchSize)
            {
                break;
            }

            page++;
        }
    }
}
