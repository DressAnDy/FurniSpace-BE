using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.ProjectChats;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectChatRepository : GenericRepository<ProjectChat>, IProjectChatRepository
{
    public ProjectChatRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<ProjectChatAccessReadModel?> GetAccessAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectSet
            .Where(project => project.ProjectId == projectId)
            .Select(project => new ProjectChatAccessReadModel
            {
                ProjectId = project.ProjectId,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                RoleName = DbContext.AccountSet
                    .Where(account => account.AccountId == currentUserId && account.DeletedAt == null)
                    .Join(
                        DbContext.RoleSet,
                        account => account.RoleId,
                        role => role.RoleId,
                        (_, role) => role.RoleName)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ProjectChatListItemReadModel> Items, int Total)> GetListAsync(
        Guid projectId,
        ProjectChatListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var chats = DbContext.ProjectChatSet.Where(chat => chat.ProjectId == projectId);

        if (query.Status.HasValue)
        {
            chats = chats.Where(chat => chat.Status == query.Status);
        }
        else
        {
            chats = chats.Where(chat => chat.Status == null || chat.Status != ProjectChatStatus.ARCHIVED);
        }

        if (query.ChatType.HasValue)
        {
            chats = chats.Where(chat => chat.ChatType == query.ChatType);
        }

        if (query.AllowedChatTypes is not null)
        {
            chats = chats.Where(chat => query.AllowedChatTypes.Contains(chat.ChatType));
        }

        var total = await chats.CountAsync(cancellationToken);
        var items = await chats
            .OrderByDescending(chat => chat.CreatedAt)
            .ThenBy(chat => chat.ChatId)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(chat => new ProjectChatListItemReadModel
            {
                ChatId = chat.ChatId,
                ProjectId = chat.ProjectId,
                ChatType = chat.ChatType,
                StaffId = chat.StaffId,
                StaffName = DbContext.AccountSet
                    .Where(account => account.AccountId == chat.StaffId)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                Title = chat.Title,
                Status = chat.Status,
                LastMessage = DbContext.ProjectChatMessageSet
                    .Where(message => message.ChatId == chat.ChatId && message.DeletedAt == null)
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.MessageId)
                    .Select(message => new ProjectChatLastMessageReadModel
                    {
                        MessageId = message.MessageId,
                        SenderId = message.SenderId,
                        SenderName = DbContext.AccountSet
                            .Where(account => account.AccountId == message.SenderId)
                            .Select(account => account.FullName)
                            .FirstOrDefault(),
                        MessageType = message.MessageType,
                        ContentPreview = message.Content != null && message.Content.Length > 200
                            ? message.Content.Substring(0, 200)
                            : message.Content,
                        CreatedAt = message.CreatedAt
                    })
                    .FirstOrDefault(),
                CreatedAt = chat.CreatedAt,
                ClosedAt = chat.ClosedAt
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ProjectChat?> GetActiveAsync(
        Guid projectId,
        ProjectChatType chatType,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectChatSet
            .Where(chat =>
                chat.ProjectId == projectId &&
                chat.ChatType == chatType &&
                (chat.Status == null || chat.Status != ProjectChatStatus.ARCHIVED))
            .OrderByDescending(chat => chat.CreatedAt)
            .ThenBy(chat => chat.ChatId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
