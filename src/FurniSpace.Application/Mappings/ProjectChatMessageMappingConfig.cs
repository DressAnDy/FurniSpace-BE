using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.ProjectChatMessages;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectChatMessageMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectChatMessageReadModel, ProjectChatMessageDto>()
            .Map(destination => destination.MessageType, source => (source.MessageType ?? ProjectChatMessageType.TEXT).ToString());

        config.NewConfig<ProjectChatMessage, ProjectChatMessageDto>()
            .Map(destination => destination.MessageType, source => source.MessageType.ToString());

        config.NewConfig<ProjectChatMessageAttachmentReadModel, ProjectChatMessageAttachmentDto>();

        config.NewConfig<StoredFile, ProjectChatMessageAttachmentDto>()
            .Map(destination => destination.MimeType, source => source.MimeType ?? string.Empty)
            .Map(destination => destination.FileUrl, source => source.FileUrl ?? string.Empty);
    }
}
