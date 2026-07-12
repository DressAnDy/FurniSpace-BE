using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using static FurniSpace.Application.Constants.ProjectChatMessages.ProjectChatMessageServiceConstants;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Search;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.ProjectChatMessages;

public sealed class ProjectChatMessageService : IProjectChatMessageService
{
    private readonly IProjectChatMessageRepository _messages;
    private readonly IProjectFileRepository _projectFiles;
    private readonly IProjectChatRealtimeService _realtime;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProjectChatFileUploadDependencies _fileUpload;
    private readonly ILogger<ProjectChatMessageServiceDependencies> _logger;
    private readonly ISearchIndexService? _search;
    private readonly IChatMessageSearchIndexer? _chatMessageSearchIndexer;

    public ProjectChatMessageService(
        IProjectChatMessageRepository messages,
        IProjectFileRepository projectFiles,
        ProjectChatMessageServiceDependencies dependencies)
    {
        _messages = messages;
        _projectFiles = projectFiles;
        _realtime = dependencies.Realtime;
        _unitOfWork = dependencies.UnitOfWork;
        _fileUpload = dependencies.FileUpload;
        _logger = dependencies.Logger;
        _search = dependencies.Search;
        _chatMessageSearchIndexer = dependencies.ChatMessageSearchIndexer;
    }

    public async Task<bool> CanAccessChatAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (chatId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return false;
        }

        var access = await _messages.GetAccessAsync(chatId, currentUserId, cancellationToken);
        return access is not null && CanAccessChat(access, currentUserId);
    }

    public async Task<ServiceResult<ProjectChatMessageListResponseDto>> GetMessagesAsync(
        Guid chatId,
        Guid currentUserId,
        ProjectChatMessageQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var sort = NormalizeSort(query.Sort);
        var validationError = ValidateRequest(chatId, currentUserId, query, sort);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _messages.GetAccessAsync(chatId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<ProjectChatMessageListResponseDto>.NotFound("Project chat not found.");
        }

        if (!CanAccessChat(access, currentUserId))
        {
            return ServiceResult<ProjectChatMessageListResponseDto>.Forbidden(
                "You do not have access to this project chat.");
        }

        var repositoryQuery = new ProjectChatMessageQueryReadModel
        {
            Page = query.Page,
            Limit = query.Limit,
            SortDescending = string.Equals(sort, DescendingSort, StringComparison.Ordinal)
        };
        var (messageItems, total) = await _messages.GetMessagesAsync(
            chatId,
            repositoryQuery,
            cancellationToken);
        var items = messageItems.Adapt<List<ProjectChatMessageDto>>();

        for (var index = 0; index < items.Count; index++)
        {
            if (!messageItems[index].DeletedAt.HasValue)
            {
                continue;
            }

            items[index].Content = null;
            items[index].Attachment = null;
        }

        return ServiceResult<ProjectChatMessageListResponseDto>.Success(
            new ProjectChatMessageListResponseDto
            {
                Items = items,
                Page = query.Page,
                Limit = query.Limit,
                Total = total
            },
            "Chat messages retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectChatMessageSearchResponseDto>> SearchProjectMessagesAsync(
        Guid projectId,
        Guid currentUserId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageSearchResponseDto>.BadRequest("Project id and authenticated user id are required.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ServiceResult<ProjectChatMessageSearchResponseDto>.BadRequest("Search query is required.");
        }

        if (page < 1 || limit is < 1 or > 50)
        {
            return ServiceResult<ProjectChatMessageSearchResponseDto>.BadRequest("Page must be >= 1 and limit must be between 1 and 50.");
        }

        var project = await _projectFiles.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectChatMessageSearchResponseDto>.NotFound("Project not found.");
        }

        var roleName = await _projectFiles.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAccessProject(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectChatMessageSearchResponseDto>.Forbidden(
                "You do not have access to search messages for this project.");
        }

        ProjectChatMessageSearchResponseDto response;
        if (_search is not null)
        {
            try
            {
                var searchResult = await _search.SearchAsync<ChatMessageSearchDocument>(
                    ChatMessageIndexName,
                    ChatMessageElasticsearchQueryFactory.BuildProjectSearch(projectId, query, page, limit),
                    cancellationToken);

                response = new ProjectChatMessageSearchResponseDto
                {
                    Items = searchResult.Documents.Select(ChatMessageSearchResponseMapper.ToItem).ToList(),
                    Page = page,
                    Limit = limit,
                    Total = (int)Math.Min(searchResult.Total, int.MaxValue)
                };
            }
            catch
            {
                response = await GetProjectMessagesFromRepositoryAsync(projectId, query, page, limit, cancellationToken);
            }
        }
        else
        {
            response = await GetProjectMessagesFromRepositoryAsync(projectId, query, page, limit, cancellationToken);
        }

        return ServiceResult<ProjectChatMessageSearchResponseDto>.Success(
            response,
            "Project chat messages search completed successfully.");
    }

    public async Task<ServiceResult<ProjectChatMessageDto>> SendTextMessageAsync(
        Guid chatId,
        Guid currentUserId,
        SendTextChatMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSendRequest(chatId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _messages.GetAccessAsync(chatId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<ProjectChatMessageDto>.NotFound("Project chat not found.");
        }

        if (!CanAccessChat(access, currentUserId))
        {
            return ServiceResult<ProjectChatMessageDto>.Forbidden(
                "You do not have access to this project chat.");
        }

        if ((access.ChatStatus ?? ProjectChatStatus.OPEN) != ProjectChatStatus.OPEN)
        {
            return ServiceResult<ProjectChatMessageDto>.Conflict(
                "Messages can only be sent to an open project chat.");
        }

        var message = new ProjectChatMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = currentUserId,
            MessageType = ProjectChatMessageType.TEXT,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _messages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SyncChatMessageIndexAsync(message.MessageId, cancellationToken);

        var response = MapCreatedMessage(message, access);

        try
        {
            await _realtime.SendMessageSentAsync(
                access.ProjectId,
                chatId,
                response,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish project chat message {MessageId} for chat {ChatId}",
                message.MessageId,
                chatId);
        }

        return ServiceResult<ProjectChatMessageDto>.Created(
            response,
            "Message sent successfully.");
    }

    public async Task<ServiceResult<ProjectChatMessageDto>> SendFileMessageAsync(
        Guid chatId,
        Guid currentUserId,
        SendFileChatMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSendFileRequest(chatId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _messages.GetAccessAsync(chatId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<ProjectChatMessageDto>.NotFound("Project chat not found.");
        }

        if (!CanAccessChat(access, currentUserId))
        {
            return ServiceResult<ProjectChatMessageDto>.Forbidden(
                "You do not have access to this project chat.");
        }

        if ((access.ChatStatus ?? ProjectChatStatus.OPEN) != ProjectChatStatus.OPEN)
        {
            return ServiceResult<ProjectChatMessageDto>.Conflict(
                "Messages can only be sent to an open project chat.");
        }

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            _fileUpload.FirebaseSettings,
            access.ProjectId,
            generatedFileName);
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            request.Visibility,
            access.RoleName,
            CustomerRole);
        var normalizedContent = ProjectFileUploadSupport.NormalizeOptionalText(request.Content);

        var uploadResult = await _fileUpload.Storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = request.FileContent,
                ObjectName = objectName,
                ContentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType)
            },
            cancellationToken);

        var storedFile = ProjectFileUploadSupport.CreateStoredFile(
            new StoredFileCreationRequest(
                fileId,
                currentUserId,
                originalFileName,
                generatedFileName,
                uploadResult,
                request.ContentType,
                request.FileSizeBytes,
                now));

        var fileLink = ProjectFileUploadSupport.CreateProjectFileLink(
            new ProjectFileLinkCreationRequest(
                fileLinkId,
                fileId,
                access.ProjectId,
                request.FileType,
                visibility,
                normalizedContent,
                currentUserId,
                now));

        var message = new ProjectChatMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = currentUserId,
            MessageType = ProjectChatMessageType.FILE,
            Content = normalizedContent,
            AttachmentFileId = fileId,
            CreatedAt = now
        };

        try
        {
            await ExecuteInTransactionAsync(
                async ct =>
                {
                    await _projectFiles.AddAsync(storedFile, ct);
                    await _projectFiles.AddFileLinkAsync(fileLink, ct);
                    await _messages.AddAsync(message, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
        }
        catch
        {
            await _fileUpload.Storage.DeleteAsync(uploadResult.ObjectName, cancellationToken);
            throw;
        }

        await SyncChatMessageIndexAsync(message.MessageId, cancellationToken);

        var response = MapCreatedMessage(message, access, storedFile);

        try
        {
            await _realtime.SendMessageSentAsync(
                access.ProjectId,
                chatId,
                response,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish project chat file message {MessageId} for chat {ChatId}",
                message.MessageId,
                chatId);
        }

        return ServiceResult<ProjectChatMessageDto>.Created(
            response,
            "File message sent successfully.");
    }

    private static ServiceResult<ProjectChatMessageListResponseDto>? ValidateRequest(
        Guid chatId,
        Guid currentUserId,
        ProjectChatMessageQueryDto query,
        string sort)
    {
        if (chatId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageListResponseDto>.BadRequest("Chat id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageListResponseDto>.Unauthorized(
                "Authenticated account id is required.");
        }

        if (query.Page < 1)
        {
            return ServiceResult<ProjectChatMessageListResponseDto>.BadRequest(
                "Page must be greater than zero.");
        }

        if (query.Limit is < 1 or > 100)
        {
            return ServiceResult<ProjectChatMessageListResponseDto>.BadRequest(
                "Limit must be between 1 and 100.");
        }

        return sort is not AscendingSort and not DescendingSort
            ? ServiceResult<ProjectChatMessageListResponseDto>.BadRequest(
                "Sort must be ASC or DESC.")
            : null;
    }

    private static ServiceResult<ProjectChatMessageDto>? ValidateSendRequest(
        Guid chatId,
        Guid currentUserId,
        SendTextChatMessageRequestDto request)
    {
        if (chatId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest("Chat id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageDto>.Unauthorized(
                "Authenticated account id is required.");
        }

        if (!Enum.IsDefined(typeof(ProjectChatMessageType), request.MessageType) ||
            request.MessageType != ProjectChatMessageType.TEXT)
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest(
                "Message type must be TEXT.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest(
                "Message content is required.");
        }

        return request.Content.Trim().Length > MaxTextMessageLength
            ? ServiceResult<ProjectChatMessageDto>.BadRequest(
                "Message content must not exceed 4000 characters.")
            : null;
    }

    private ServiceResult<ProjectChatMessageDto>? ValidateSendFileRequest(
        Guid chatId,
        Guid currentUserId,
        SendFileChatMessageRequestDto request)
    {
        if (chatId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest("Chat id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageDto>.Unauthorized(
                "Authenticated account id is required.");
        }

        var fileValidation = _fileUpload.FileUploadValidator.Validate(request);
        if (!fileValidation.IsValid)
        {
            return MapFileValidationResult(fileValidation);
        }

        if (!string.IsNullOrWhiteSpace(request.Content) &&
            request.Content.Trim().Length > MaxTextMessageLength)
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest(
                "Message content must not exceed 4000 characters.");
        }

        return null;
    }

    private static ServiceResult<ProjectChatMessageDto> MapFileValidationResult(
        FileUploadValidationResult validation)
    {
        return validation.FailureKind switch
        {
            FileUploadValidationFailureKind.FileTooLarge =>
                ServiceResult<ProjectChatMessageDto>.PayloadTooLarge(validation.Message),
            FileUploadValidationFailureKind.InvalidExtension or FileUploadValidationFailureKind.InvalidMimeType =>
                ServiceResult<ProjectChatMessageDto>.UnsupportedMediaType(validation.Message),
            _ => ServiceResult<ProjectChatMessageDto>.BadRequest(validation.Message)
        };
    }

    private static ProjectChatMessageDto MapCreatedMessage(
        ProjectChatMessage message,
        ProjectChatMessageAccessReadModel access,
        StoredFile? attachmentFile = null)
    {
        var response = message.Adapt<ProjectChatMessageDto>();
        response.SenderName = access.CurrentUserName;
        response.SenderRole = access.RoleName;

        if (attachmentFile is not null)
        {
            response.Attachment = attachmentFile.Adapt<ProjectChatMessageAttachmentDto>();
        }

        return response;
    }

    private static bool CanAccessChat(
        ProjectChatMessageAccessReadModel access,
        Guid currentUserId)
    {
        if (IsRole(access.RoleName, AdminRole))
        {
            return true;
        }

        if (IsRole(access.RoleName, CustomerRole))
        {
            return access.CustomerId == currentUserId &&
                IsCustomerOrSalesVisible(access.ChatType);
        }

        if (IsRole(access.RoleName, SalesRole))
        {
            return access.AssignedSalesId == currentUserId &&
                IsCustomerOrSalesVisible(access.ChatType);
        }

        return IsRole(access.RoleName, DesignerRole) &&
            access.AssignedDesignerId == currentUserId &&
            access.ChatType == ProjectChatType.DESIGNER;
    }

    private static bool IsCustomerOrSalesVisible(ProjectChatType chatType)
    {
        return chatType is ProjectChatType.SALES or ProjectChatType.DESIGNER;
    }

    private static bool IsRole(string? roleName, string expectedRole)
    {
        return string.Equals(roleName, expectedRole, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSort(string? sort)
    {
        return sort?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<ProjectChatMessageSearchResponseDto> GetProjectMessagesFromRepositoryAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = await _messages.SearchByProjectAsync(projectId, query, page, limit, cancellationToken);
        var total = await _messages.CountSearchByProjectAsync(projectId, query, cancellationToken);

        return new ProjectChatMessageSearchResponseDto
        {
            Items = items
                .Where(item => item.Content is not null)
                .Select(item => new ProjectChatMessageSearchItemDto
                {
                    MessageId = item.MessageId,
                    ChatId = item.ChatId,
                    ProjectId = item.ProjectId,
                    SenderId = item.SenderId,
                    SenderName = item.SenderName,
                    MessageType = item.MessageType?.ToString(),
                    Content = item.Content!,
                    CreatedAt = item.CreatedAt
                })
                .ToList(),
            Page = page,
            Limit = limit,
            Total = total
        };
    }

    private Task SyncChatMessageIndexAsync(Guid messageId, CancellationToken cancellationToken)
    {
        return _chatMessageSearchIndexer?.SyncMessageAsync(messageId, cancellationToken) ?? Task.CompletedTask;
    }

    private static bool CanAccessProject(
        ProjectFileAccessReadModel project,
        Guid currentUserId,
        string? roleName)
    {
        if (IsRole(roleName, AdminRole))
        {
            return true;
        }

        if (IsRole(roleName, CustomerRole))
        {
            return project.CustomerId == currentUserId;
        }

        if (IsRole(roleName, SalesRole))
        {
            return project.AssignedSalesId == currentUserId;
        }

        if (IsRole(roleName, DesignerRole))
        {
            return project.AssignedDesignerId == currentUserId;
        }

        return false;
    }
}
