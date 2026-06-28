#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;

namespace FurniSpace.Application.Tests.TestDoubles;

internal static class ProjectFileRepositorySearchStubs
{
    public static Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        _ = fileId;
        _ = cancellationToken;
        return Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);
    }

    public static Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = page;
        _ = limit;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
    }

    public static Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default)
    {
        _ = projectId;
        _ = query;
        _ = page;
        _ = limit;
        _ = customerVisibleOnly;
        _ = customerAccountId;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
    }

    public static Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default)
    {
        _ = projectId;
        _ = query;
        _ = customerVisibleOnly;
        _ = customerAccountId;
        _ = cancellationToken;
        return Task.FromResult(0);
    }
}

internal static class ProjectChatMessageRepositorySearchStubs
{
    public static Task<ChatMessageSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        _ = messageId;
        _ = cancellationToken;
        return Task.FromResult<ChatMessageSearchIndexItemReadModel?>(null);
    }

    public static Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = page;
        _ = limit;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ChatMessageSearchIndexItemReadModel>>([]);
    }

    public static Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _ = projectId;
        _ = query;
        _ = page;
        _ = limit;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ChatMessageSearchIndexItemReadModel>>([]);
    }

    public static Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
    {
        _ = projectId;
        _ = query;
        _ = cancellationToken;
        return Task.FromResult(0);
    }
}
