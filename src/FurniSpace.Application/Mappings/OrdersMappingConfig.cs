using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Infrastructure.ReadModels.Orders;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class OrdersMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OrderListItemReadModel, OrderListItemDto>();
        config.NewConfig<OrderDetailReadModel, OrderDetailDto>();
        config.NewConfig<OrderItemDetailReadModel, OrderItemDto>();
    }
}
