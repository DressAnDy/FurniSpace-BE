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
        config.NewConfig<OrderItemDetailReadModel, OrderItemDto>()
            .Map(
                destination => destination.RemainingDeliveryQuantity,
                source => Math.Max(0, (source.Quantity ?? 0) - source.DeliveredQuantity));
        config.NewConfig<DeliveryScheduleSummaryReadModel, DeliveryScheduleSummaryDto>();
        config.NewConfig<DeliveryListItemReadModel, DeliveryListItemDto>();
        config.NewConfig<DeliveryDetailReadModel, DeliveryDetailDto>();
        config.NewConfig<DeliveryItemReadModel, DeliveryItemDto>();
        config.NewConfig<OrderDeliveryTrackingReadModel, OrderDeliveryTrackingDto>()
            .Map(
                destination => destination.Summary,
                source => new OrderDeliveryTrackingSummaryDto
                {
                    TotalOrderedQuantity = source.TotalOrderedQuantity,
                    TotalDeliveredQuantity = source.TotalDeliveredQuantity,
                    RemainingQuantity = source.RemainingQuantity,
                    DeliveryProgressPercent = source.DeliveryProgressPercent,
                    CompletedDeliveryCount = source.CompletedDeliveryCount,
                    UpcomingDeliveryCount = source.UpcomingDeliveryCount,
                    NextDeliveryAt = source.NextDeliveryAt
                });
        config.NewConfig<OrderDeliveryTrackingItemReadModel, OrderDeliveryTrackingItemDto>();
        config.NewConfig<OrderDeliveryTrackingTimelineEntryReadModel, OrderDeliveryTrackingTimelineEntryDto>();
        config.NewConfig<OrderDeliveryTrackingTimelineItemReadModel, OrderDeliveryTrackingTimelineItemDto>();
        config.NewConfig<ProjectDeliverySummaryReadModel, ProjectDeliverySummaryDto>();
    }
}
