using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
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
    private readonly IUnitOfWork _unitOfWork;

    public ProjectChatService(IProjectChatRepository chats, IUnitOfWork unitOfWork)
    {
        _chats = chats;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> CanAccessProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return false;
        }

        var access = await _chats.GetAccessAsync(projectId, currentUserId, cancellationToken);
        return access is not null && CanAccessProject(access, currentUserId);
    }

    public async Task<ServiceResult<ProjectChatSummaryDto>> CreateManualAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateManualCreateRequest(projectId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _chats.GetAccessAsync(projectId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<ProjectChatSummaryDto>.NotFound("Project not found.");
        }

        if (!IsRole(access.RoleName, AdminRole))
        {
            return ServiceResult<ProjectChatSummaryDto>.Forbidden(
                "Only administrators can manually create project chats.");
        }

        var activeChat = await _chats.GetActiveAsync(projectId, request.ChatType, cancellationToken);
        if (activeChat is not null)
        {
            return ServiceResult<ProjectChatSummaryDto>.Conflict(
                "An active project chat with the same type already exists.");
        }

        var chat = new ProjectChat
        {
            ChatId = Guid.NewGuid(),
            ProjectId = projectId,
            ChatType = request.ChatType,
            StaffId = request.StaffId,
            Title = request.Title.Trim(),
            Status = ProjectChatStatus.OPEN,
            CreatedAt = DateTime.UtcNow
        };

        await _chats.AddAsync(chat, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectChatSummaryDto>.Created(
            ToSummaryDto(chat),
            "Project chat created successfully.");
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

    public async Task<ServiceResult<ProjectChatSummaryDto>> UpdateStatusAsync(
        Guid chatId,
        Guid currentUserId,
        UpdateProjectChatStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateUpdateStatusRequest(chatId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _chats.GetStatusAccessAsync(chatId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<ProjectChatSummaryDto>.NotFound("Project chat not found.");
        }

        if (IsRole(access.RoleName, CustomerRole))
        {
            return ServiceResult<ProjectChatSummaryDto>.Forbidden(
                "Customers cannot close project chats.");
        }

        if (!CanCloseChat(access, currentUserId))
        {
            return ServiceResult<ProjectChatSummaryDto>.Forbidden(
                "You do not have permission to close this project chat.");
        }

        var currentStatus = access.ChatStatus ?? ProjectChatStatus.OPEN;
        if (currentStatus == ProjectChatStatus.CLOSED)
        {
            return ServiceResult<ProjectChatSummaryDto>.Conflict("Project chat is already closed.");
        }

        if (currentStatus != ProjectChatStatus.OPEN)
        {
            return ServiceResult<ProjectChatSummaryDto>.Conflict(
                "Only open project chats can be closed.");
        }

        var chat = await _chats.GetByIdAsync(chatId, cancellationToken);
        if (chat is null)
        {
            return ServiceResult<ProjectChatSummaryDto>.NotFound("Project chat not found.");
        }

        chat.Status = ProjectChatStatus.CLOSED;
        chat.ClosedAt = DateTime.UtcNow;
        _chats.Update(chat);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectChatSummaryDto>.Success(
            ToSummaryDto(chat),
            "Project chat closed successfully.");
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

    private static ServiceResult<ProjectChatSummaryDto>? ValidateManualCreateRequest(
        Guid projectId,
        Guid currentUserId,
        CreateProjectChatRequestDto request)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatSummaryDto>.Unauthorized("Authenticated account id is required.");
        }

        if (!Enum.IsDefined(typeof(ProjectChatType), request.ChatType))
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Project chat type is invalid.");
        }

        if (request.ChatType is ProjectChatType.SALES or ProjectChatType.DESIGNER)
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest(
                "Sales and Designer chats must be created through project assignment.");
        }

        if (request.StaffId == Guid.Empty)
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Staff id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Chat title is required.");
        }

        return request.Title.Trim().Length > MaxTitleLength
            ? ServiceResult<ProjectChatSummaryDto>.BadRequest(
                "Chat title must not exceed 150 characters.")
            : null;
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

    private static ServiceResult<ProjectChatSummaryDto>? ValidateUpdateStatusRequest(
        Guid chatId,
        Guid currentUserId,
        UpdateProjectChatStatusRequestDto request)
    {
        if (chatId == Guid.Empty)
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Chat id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatSummaryDto>.Unauthorized("Authenticated account id is required.");
        }

        if (!request.Status.HasValue)
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Project chat status is required.");
        }

        if (!Enum.IsDefined(typeof(ProjectChatStatus), request.Status.Value))
        {
            return ServiceResult<ProjectChatSummaryDto>.BadRequest("Project chat status is invalid.");
        }

        return request.Status.Value != ProjectChatStatus.CLOSED
            ? ServiceResult<ProjectChatSummaryDto>.BadRequest(
                "Only closing a project chat is supported.")
            : null;
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

    private static bool CanCloseChat(ProjectChatStatusAccessReadModel access, Guid currentUserId)
    {
        if (IsRole(access.RoleName, AdminRole))
        {
            return true;
        }

        return access.ChatType switch
        {
            ProjectChatType.SALES =>
                IsRole(access.RoleName, SalesRole) && access.AssignedSalesId == currentUserId,
            ProjectChatType.DESIGNER =>
                (IsRole(access.RoleName, DesignerRole) && access.AssignedDesignerId == currentUserId) ||
                (IsRole(access.RoleName, SalesRole) && access.AssignedSalesId == currentUserId),
            _ => false
        };
    }

    private static ProjectChatType[]? GetVisibleChatTypes(string? roleName)
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
