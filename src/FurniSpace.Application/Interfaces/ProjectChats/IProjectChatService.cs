using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Interfaces.ProjectChats;

public interface IProjectChatService
{
    Task<bool> CanAccessProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectChatSummaryDto>> CreateManualAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectChatRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectChatListResponseDto>> GetProjectChatsAsync(
        Guid projectId,
        Guid currentUserId,
        ProjectChatListQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ProjectChatSummaryDto> UpsertProjectChatAsync(
        Guid projectId,
        ProjectChatType chatType,
        Guid staffId,
        string title,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectChatSummaryDto>> UpdateStatusAsync(
        Guid chatId,
        Guid currentUserId,
        UpdateProjectChatStatusRequestDto request,
        CancellationToken cancellationToken = default);
}
