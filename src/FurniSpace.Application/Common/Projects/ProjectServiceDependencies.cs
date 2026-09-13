using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Projects;

public sealed record ProjectServiceDependencies(
    IUnitOfWork UnitOfWork,
    ProjectStatusTransitionEvaluator TransitionEvaluator,
    INotificationDispatcher? Notifications,
    ILogger<ProjectService>? Logger,
    IProjectChatService? ProjectChats,
    IPaymentRepository Payments,
    IOrderRepository Orders,
    IQuotationRepository Quotations,
    IProposalRepository Proposals,
    IProductionRequestRepository ProductionRequests,
    IProjectScheduleRepository Schedules,
    IDeliveryRepository Deliveries,
    IProjectPhaseDeadlineService PhaseDeadlines);
