using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using Microsoft.Extensions.Logging;
using static FurniSpace.Application.Constants.ProjectChatMessages.ProjectChatMessageServiceConstants;

namespace FurniSpace.Application.Services.ProjectChatMessages;

public sealed partial class ProjectChatMessageService
{
    public async Task<ServiceResult<PrepareProjectChatFileUploadResponseDto>> PrepareFileMessageUploadAsync(
        Guid chatId,
        Guid currentUserId,
        PrepareProjectChatFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePrepareFileUploadRequest(chatId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var access = await _messages.GetAccessAsync(chatId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ServiceResult<PrepareProjectChatFileUploadResponseDto>.NotFound("Project chat not found.");
        }

        if (!CanAccessChat(access, currentUserId))
        {
            return ServiceResult<PrepareProjectChatFileUploadResponseDto>.Forbidden(
                "You do not have access to this project chat.");
        }

        if ((access.ChatStatus ?? ProjectChatStatus.OPEN) != ProjectChatStatus.OPEN)
        {
            return ServiceResult<PrepareProjectChatFileUploadResponseDto>.Conflict(
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
        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);
        var visibility = ProjectFileUploadSupport.ResolveVisibility(
            request.Visibility,
            access.RoleName,
            ApplicationRoles.Customer);

        var storedFile = _fileUpload.DirectUploadCoordinator.CreatePendingStoredFile(
            new DirectUploadPendingFileRequest(
                fileId,
                currentUserId,
                originalFileName,
                generatedFileName,
                objectName,
                contentType,
                request.FileSizeBytes,
                now));

        var fileLink = ProjectFileUploadSupport.CreateProjectFileLink(
            new ProjectFileLinkCreationRequest(
                fileLinkId,
                fileId,
                access.ProjectId,
                request.FileType,
                visibility,
                null,
                currentUserId,
                now));

        await ExecuteInTransactionAsync(
            async ct =>
            {
                await _projectFiles.AddAsync(storedFile, ct);
                await _projectFiles.AddFileLinkAsync(fileLink, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        try
        {
            var signedUpload = await _fileUpload.DirectUploadCoordinator.CreateSignedUploadUrlAsync(
                objectName,
                contentType,
                cancellationToken);

            return ServiceResult<PrepareProjectChatFileUploadResponseDto>.Created(
                new PrepareProjectChatFileUploadResponseDto
                {
                    FileId = fileId,
                    ChatId = chatId,
                    ProjectId = access.ProjectId,
                    UploadUrl = signedUpload.UploadUrl,
                    ContentType = signedUpload.ContentType,
                    ExpiresAt = signedUpload.ExpiresAt
                },
                "Project chat file upload URL created successfully.");
        }
        catch
        {
            await _fileUpload.DirectUploadCoordinator.DeleteObjectIfExistsAsync(objectName, cancellationToken);
            _projectFiles.Remove(storedFile);
            _projectFiles.RemoveFileLinks([fileLink]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<ProjectChatMessageDto>> CompleteFileMessageUploadAsync(
        Guid chatId,
        Guid currentUserId,
        CompleteProjectChatFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
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

        if (request.FileId == Guid.Empty)
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest("File id is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Content) &&
            request.Content.Trim().Length > MaxTextMessageLength)
        {
            return ServiceResult<ProjectChatMessageDto>.BadRequest(
                "Message content must not exceed 4000 characters.");
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

        var storedFile = await _projectFiles.GetByIdAsync(request.FileId, cancellationToken);
        if (storedFile is null)
        {
            return ServiceResult<ProjectChatMessageDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Project chat file upload not found."));
        }

        var fileLinks = await _projectFiles.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken);
        var projectLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, ProjectFileUploadSupport.ProjectReferenceType, StringComparison.OrdinalIgnoreCase) &&
            link.ReferenceId == access.ProjectId);
        if (projectLink is null)
        {
            return ServiceResult<ProjectChatMessageDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Project chat file upload not found."));
        }

        if (storedFile.UploadedBy != currentUserId &&
            !IsRole(access.RoleName, ApplicationRoles.Admin))
        {
            return ServiceResult<ProjectChatMessageDto>.Failure(
                Error.Forbidden(
                    DirectFileUploadErrorCodes.UploadForbidden,
                    "You can only complete your own direct upload session."));
        }

        if (storedFile.Status == FileStatus.ACTIVE)
        {
            return await ResolveActiveFileMessageAsync(
                chatId,
                currentUserId,
                request,
                access,
                storedFile,
                cancellationToken);
        }

        if (storedFile.Status != FileStatus.PENDING)
        {
            return ServiceResult<ProjectChatMessageDto>.Failure(
                Error.Conflict(
                    DirectFileUploadErrorCodes.UploadNotPending,
                    "Project chat file upload is not pending completion."));
        }

        var finalizeResult = await _fileUpload.DirectUploadCoordinator.TryFinalizeAsync(storedFile, cancellationToken);
        if (finalizeResult.Status is not (200 or 201) || finalizeResult.Data is null)
        {
            return ServiceResult<ProjectChatMessageDto>.Failure(
                Error.Conflict(
                    finalizeResult.ErrorCode ?? DirectFileUploadErrorCodes.UploadObjectMissing,
                    finalizeResult.Message ?? "Direct upload finalization failed."));
        }

        _fileUpload.DirectUploadCoordinator.ActivateStoredFile(storedFile, finalizeResult.Data);

        await ExecuteInTransactionAsync(
            async ct =>
            {
                _projectFiles.Update(storedFile);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return await CreateFileChatMessageAsync(
            chatId,
            currentUserId,
            request,
            access,
            storedFile,
            cancellationToken);
    }

    private async Task<ServiceResult<ProjectChatMessageDto>> ResolveActiveFileMessageAsync(
        Guid chatId,
        Guid currentUserId,
        CompleteProjectChatFileUploadRequestDto request,
        ProjectChatMessageAccessReadModel access,
        StoredFile storedFile,
        CancellationToken cancellationToken)
    {
        var existingMessage = _messages.Query()
            .FirstOrDefault(message =>
                message.ChatId == chatId &&
                message.AttachmentFileId == request.FileId &&
                message.DeletedAt == null);
        if (existingMessage is not null)
        {
            return ServiceResult<ProjectChatMessageDto>.Success(
                MapCreatedMessage(existingMessage, access, storedFile),
                "File message sent successfully.");
        }

        return await CreateFileChatMessageAsync(
            chatId,
            currentUserId,
            request,
            access,
            storedFile,
            cancellationToken);
    }

    private async Task<ServiceResult<ProjectChatMessageDto>> CreateFileChatMessageAsync(
        Guid chatId,
        Guid currentUserId,
        CompleteProjectChatFileUploadRequestDto request,
        ProjectChatMessageAccessReadModel access,
        StoredFile storedFile,
        CancellationToken cancellationToken)
    {
        var normalizedContent = ProjectFileUploadSupport.NormalizeOptionalText(request.Content);
        var message = new ProjectChatMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = currentUserId,
            MessageType = ProjectChatMessageType.FILE,
            Content = normalizedContent,
            AttachmentFileId = storedFile.FileId,
            CreatedAt = DateTime.UtcNow
        };

        await _messages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        await DispatchChatNotificationAsync(access, message, response, cancellationToken);

        return ServiceResult<ProjectChatMessageDto>.Created(
            response,
            "File message sent successfully.");
    }

    private ServiceResult<PrepareProjectChatFileUploadResponseDto>? ValidatePrepareFileUploadRequest(
        Guid chatId,
        Guid currentUserId,
        PrepareProjectChatFileUploadRequestDto request)
    {
        if (chatId == Guid.Empty)
        {
            return ServiceResult<PrepareProjectChatFileUploadResponseDto>.BadRequest("Chat id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareProjectChatFileUploadResponseDto>.Unauthorized(
                "Authenticated account id is required.");
        }

        var fileValidation = _fileUpload.FileUploadValidator.ValidateMetadata(
            request.OriginalFileName,
            request.ContentType,
            request.FileSizeBytes);
        if (!fileValidation.IsValid)
        {
            return MapPrepareFileValidationResult(fileValidation);
        }

        return null;
    }

    private static ServiceResult<PrepareProjectChatFileUploadResponseDto> MapPrepareFileValidationResult(
        FileUploadValidationResult validation)
    {
        return validation.FailureKind switch
        {
            FileUploadValidationFailureKind.FileTooLarge =>
                ServiceResult<PrepareProjectChatFileUploadResponseDto>.PayloadTooLarge(validation.Message),
            FileUploadValidationFailureKind.InvalidExtension or FileUploadValidationFailureKind.InvalidMimeType =>
                ServiceResult<PrepareProjectChatFileUploadResponseDto>.UnsupportedMediaType(validation.Message),
            _ => ServiceResult<PrepareProjectChatFileUploadResponseDto>.BadRequest(validation.Message)
        };
    }
}
