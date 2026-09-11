using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Orders;

public sealed record OrderServiceDependencies(
    IProductionRequestRepository ProductionRequests,
    IProjectScheduleRepository Schedules,
    IDeliveryRepository Deliveries,
    IUnitOfWork UnitOfWork,
    SePayOptions SePayOptions,
    INotificationDispatcher? Notifications,
    ILogger? Logger);
