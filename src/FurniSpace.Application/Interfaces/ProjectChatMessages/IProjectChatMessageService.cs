using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChatMessages;

namespace FurniSpace.Application.Interfaces.ProjectChatMessages;

public interface IProjectChatMessageService
{
    Task<bool> CanAccessChatAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectChatMessageListResponseDto>> GetMessagesAsync(
        Guid chatId,
        Guid currentUserId,
        ProjectChatMessageQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectChatMessageDto>> SendTextMessageAsync(
        Guid chatId,
        Guid currentUserId,
        SendTextChatMessageRequestDto request,
        CancellationToken cancellationToken = default);
}
