using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectChatMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectChat, ProjectChatSummaryDto>()
            .Map(destination => destination.ChatType, source => source.ChatType.ToString())
            .Map(
                destination => destination.Status,
                source => (source.Status ?? ProjectChatStatus.OPEN).ToString());
    }
}
