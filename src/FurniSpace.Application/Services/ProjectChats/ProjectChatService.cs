using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.DTOs.ProjectChats;
using Mapster;

namespace FurniSpace.Application.Services.ProjectChats;

public sealed class ProjectChatService : IProjectChatService
{
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string SalesRole = "SALES";
    private const int MaxTitleLength = 150;
    private static readonly ProjectChatType[] CustomerAndSalesChatTypes =
        [ProjectChatType.SALES, ProjectChatType.DESIGNER];
    private static readonly ProjectChatType[] DesignerChatTypes = [ProjectChatType.DESIGNER];
    private readonly IProjectChatRepository _chats;

    public ProjectChatService(IProjectChatRepository chats)
    {
        _chats = chats;
    }

    public async Task<ServiceResult<ProjectChatListResponseDto>> GetProjectChatsAsync(
        Guid projectId,
        Guid currentUserId,
        ProjectChatListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateListRequest(projectId, currentUserId, query);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _chats.GetAccessAsync(projectId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<ProjectChatListResponseDto>.NotFound("Project not found.");
        }

        if (!CanAccessProject(access, currentUserId))
        {
            return ServiceResult<ProjectChatListResponseDto>.Forbidden(
                "You do not have access to view chats for this project.");
        }

        var repositoryQuery = query.Adapt<ProjectChatListQueryReadModel>();
        repositoryQuery.AllowedChatTypes = GetVisibleChatTypes(access.RoleName);
        var (chatItems, total) = await _chats.GetListAsync(projectId, repositoryQuery, cancellationToken);
        var items = chatItems.Adapt<List<ProjectChatListItemDto>>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var chatItem = chatItems[index];
            item.ChatType = chatItem.ChatType.ToString();
            item.Status = (chatItem.Status ?? ProjectChatStatus.OPEN).ToString();
            var lastMessage = item.LastMessage;
            var lastMessageReadModel = chatItem.LastMessage;

            if (lastMessage is not null && lastMessageReadModel is not null)
            {
                lastMessage.MessageType =
                    (lastMessageReadModel.MessageType ?? ProjectChatMessageType.TEXT).ToString();
            }
        }

        return ServiceResult<ProjectChatListResponseDto>.Success(
            new ProjectChatListResponseDto
            {
                Items = items,
                Page = query.Page,
                Limit = query.Limit,
                Total = total
            },
            "Project chats retrieved successfully.");
    }

    public async Task<ProjectChatSummaryDto> UpsertProjectChatAsync(
        Guid projectId,
        ProjectChatType chatType,
        Guid staffId,
        string title,
        CancellationToken cancellationToken = default)
    {
        Validate(projectId, chatType, staffId, title);

        var chat = await _chats.GetActiveAsync(projectId, chatType, cancellationToken);
        if (chat is null)
        {
            chat = new ProjectChat
            {
                ChatId = Guid.NewGuid(),
                ProjectId = projectId,
                ChatType = chatType,
                StaffId = staffId,
                Title = title.Trim(),
                Status = ProjectChatStatus.OPEN,
                CreatedAt = DateTime.UtcNow
            };

            await _chats.AddAsync(chat, cancellationToken);
        }
        else
        {
            chat.StaffId = staffId;
            _chats.Update(chat);
        }

        return ToSummaryDto(chat);
    }

    private static void Validate(
        Guid projectId,
        ProjectChatType chatType,
        Guid staffId,
        string title)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        if (!Enum.IsDefined(typeof(ProjectChatType), chatType))
        {
            throw new ArgumentOutOfRangeException(nameof(chatType), chatType, "Project chat type is invalid.");
        }

        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("Staff id is required.", nameof(staffId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Chat title is required.", nameof(title));
        }

        if (title.Trim().Length > MaxTitleLength)
        {
            throw new ArgumentException("Chat title must not exceed 150 characters.", nameof(title));
        }
    }

    private static ServiceResult<ProjectChatListResponseDto>? ValidateListRequest(
        Guid projectId,
        Guid currentUserId,
        ProjectChatListQueryDto query)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectChatListResponseDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatListResponseDto>.Unauthorized("Authenticated account id is required.");
        }

        if (query.Page < 1)
        {
            return ServiceResult<ProjectChatListResponseDto>.BadRequest("Page must be greater than zero.");
        }

        if (query.Limit is < 1 or > 100)
        {
            return ServiceResult<ProjectChatListResponseDto>.BadRequest("Limit must be between 1 and 100.");
        }

        if (query.Status.HasValue && !Enum.IsDefined(typeof(ProjectChatStatus), query.Status.Value))
        {
            return ServiceResult<ProjectChatListResponseDto>.BadRequest("Project chat status is invalid.");
        }

        if (query.ChatType.HasValue && !Enum.IsDefined(typeof(ProjectChatType), query.ChatType.Value))
        {
            return ServiceResult<ProjectChatListResponseDto>.BadRequest("Project chat type is invalid.");
        }

        return null;
    }

    private static bool CanAccessProject(ProjectChatAccessReadModel access, Guid currentUserId)
    {
        if (IsRole(access.RoleName, AdminRole))
        {
            return true;
        }

        if (IsRole(access.RoleName, CustomerRole))
        {
            return access.CustomerId == currentUserId;
        }

        if (IsRole(access.RoleName, SalesRole))
        {
            return access.AssignedSalesId == currentUserId;
        }

        return IsRole(access.RoleName, DesignerRole) &&
            access.AssignedDesignerId == currentUserId;
    }

    private static IReadOnlyCollection<ProjectChatType>? GetVisibleChatTypes(string? roleName)
    {
        if (IsRole(roleName, AdminRole))
        {
            return null;
        }

        return IsRole(roleName, DesignerRole)
            ? DesignerChatTypes
            : CustomerAndSalesChatTypes;
    }

    private static bool IsRole(string? roleName, string expectedRole)
    {
        return string.Equals(roleName, expectedRole, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectChatSummaryDto ToSummaryDto(ProjectChat chat)
    {
        var dto = chat.Adapt<ProjectChatSummaryDto>();
        dto.ChatType = chat.ChatType.ToString();
        dto.Status = (chat.Status ?? ProjectChatStatus.OPEN).ToString();
        return dto;
    }
}
