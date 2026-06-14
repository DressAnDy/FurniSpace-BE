using FurniSpace.Application.DTOs.Notifications;
using FurniSpace.Domain.Entities;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class NotificationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Notification, NotificationDto>();
    }
}
