using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.ProjectChatMessages;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.ProjectChatMessages;

public sealed class ProjectChatMessageService : IProjectChatMessageService
{
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string DesignerRole = "DESIGNER";
    private const string SalesRole = "SALES";
    private const string AscendingSort = "ASC";
    private const string DescendingSort = "DESC";
    private const int MaxTextMessageLength = 4000;
    private readonly IProjectChatMessageRepository _messages;
    private readonly IProjectFileRepository _projectFiles;
    private readonly IProjectChatRealtimeService _realtime;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProjectChatFileUploadDependencies _fileUpload;
    private readonly ILogger<ProjectChatMessageService> _logger;

    public ProjectChatMessageService(
        IProjectChatMessageRepository messages,
        IProjectFileRepository projectFiles,
        IProjectChatRealtimeService realtime,
        IUnitOfWork unitOfWork,
        ProjectChatFileUploadDependencies fileUpload,
        ILogger<ProjectChatMessageService> logger)
    {
        _messages = messages;
        _projectFiles = projectFiles;
        _realtime = realtime;
        _unitOfWork = unitOfWork;
        _fileUpload = fileUpload;
        _logger = logger;
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
}
