using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.ProjectChatMessages;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectChatMessageRepository
    : GenericRepository<ProjectChatMessage>, IProjectChatMessageRepository
{
    public ProjectChatMessageRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<ProjectChatMessageAccessReadModel?> GetAccessAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectChatSet
            .Where(chat => chat.ChatId == chatId)
            .Join(
                DbContext.ProjectSet,
                chat => chat.ProjectId,
                project => project.ProjectId,
                (chat, project) => new ProjectChatMessageAccessReadModel
                {
                    ChatId = chat.ChatId,
                    ProjectId = project.ProjectId,
                    ChatType = chat.ChatType,
                    ChatStatus = chat.Status,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId,
                    CurrentUserName = DbContext.AccountSet
                        .Where(account =>
                            account.AccountId == currentUserId &&
                            account.DeletedAt == null)
                        .Select(account => account.FullName)
                        .FirstOrDefault(),
                    RoleName = DbContext.AccountSet
                        .Where(account =>
                            account.AccountId == currentUserId &&
                            account.DeletedAt == null)
                        .Join(
                            DbContext.RoleSet,
                            account => account.RoleId,
                            role => role.RoleId,
                            (_, role) => role.RoleName)
                        .FirstOrDefault()
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ProjectChatMessageReadModel> Items, int Total)> GetMessagesAsync(
        Guid chatId,
        ProjectChatMessageQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var messages = DbContext.ProjectChatMessageSet.Where(message => message.ChatId == chatId);
        var total = await messages.CountAsync(cancellationToken);
        var orderedMessages = query.SortDescending
            ? messages.OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.MessageId)
            : messages.OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.MessageId);

        var items = await orderedMessages
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(message => new ProjectChatMessageReadModel
            {
                MessageId = message.MessageId,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                SenderName = DbContext.AccountSet
                    .Where(account => account.AccountId == message.SenderId)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                SenderRole = DbContext.AccountSet
                    .Where(account => account.AccountId == message.SenderId)
                    .Join(
                        DbContext.RoleSet,
                        account => account.RoleId,
                        role => role.RoleId,
                        (_, role) => role.RoleName)
                    .FirstOrDefault(),
                MessageType = message.MessageType,
                Content = message.DeletedAt == null ? message.Content : null,
                Attachment = message.DeletedAt != null || message.AttachmentFileId == null
                    ? null
                    : DbContext.StoredFileSet
                        .Where(file => file.FileId == message.AttachmentFileId)
                        .Select(file => new ProjectChatMessageAttachmentReadModel
                        {
                            FileId = file.FileId,
                            OriginalFileName = file.OriginalFileName,
                            MimeType = file.MimeType,
                            FileSizeBytes = file.FileSizeBytes,
                            FileUrl = file.FileUrl
                        })
                        .FirstOrDefault(),
                CreatedAt = message.CreatedAt,
                EditedAt = message.EditedAt,
                DeletedAt = message.DeletedAt,
                ReadAt = message.ReadAt
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ChatMessageSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return BuildSearchIndexQuery()
            .Where(item => item.MessageId == messageId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await BuildSearchIndexQuery()
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.MessageId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pattern = $"%{query.Trim()}%";
        return await BuildSearchIndexQuery()
            .Where(item => item.ProjectId == projectId)
            .Where(item => item.DeletedAt == null)
            .Where(item => item.Content != null && EF.Functions.ILike(item.Content, pattern))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.MessageId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var pattern = $"%{query.Trim()}%";
        return BuildSearchIndexQuery()
            .Where(item => item.ProjectId == projectId)
            .Where(item => item.DeletedAt == null)
            .Where(item => item.Content != null && EF.Functions.ILike(item.Content, pattern))
            .CountAsync(cancellationToken);
    }

    private IQueryable<ChatMessageSearchIndexItemReadModel> BuildSearchIndexQuery()
    {
        return DbContext.ProjectChatMessageSet
            .Join(
                DbContext.ProjectChatSet,
                message => message.ChatId,
                chat => chat.ChatId,
                (message, chat) => new { message, chat })
            .Select(entry => new ChatMessageSearchIndexItemReadModel
            {
                MessageId = entry.message.MessageId,
                ChatId = entry.message.ChatId,
                ProjectId = entry.chat.ProjectId,
                SenderId = entry.message.SenderId,
                SenderName = DbContext.AccountSet
                    .Where(account => account.AccountId == entry.message.SenderId)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                MessageType = entry.message.MessageType,
                Content = entry.message.Content,
                CreatedAt = entry.message.CreatedAt,
                DeletedAt = entry.message.DeletedAt
            });
    }
}
