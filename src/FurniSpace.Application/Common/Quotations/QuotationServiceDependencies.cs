using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Quotations;
using FurniSpace.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Quotations;

public sealed record QuotationServiceDependencies(
    IUnitOfWork UnitOfWork,
    OrderWorkflowSettings OrderWorkflowSettings,
    QuotationRecalculationService RecalculationService,
    INotificationDispatcher? Notifications,
    ILogger<QuotationService>? Logger);
