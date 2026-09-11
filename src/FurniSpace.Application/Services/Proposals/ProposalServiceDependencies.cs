using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Interfaces.Quotations;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;
using RoomPlannerSceneRepository = FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository;

namespace FurniSpace.Application.Services.Proposals;

public sealed class ProposalServiceDependencies
{
    public ProposalServiceDependencies(
        RoomPlannerSceneRepository? roomPlannerScenes = null,
        INotificationDispatcher? notifications = null,
        ILogger<ProposalService>? logger = null,
        ICustomizationRequestRepository? customizationRequests = null,
        IQuotationService? quotations = null,
        IProjectPhaseDeadlineService? phaseDeadlines = null)
    {
        RoomPlannerScenes = roomPlannerScenes;
        Notifications = notifications;
        Logger = logger;
        CustomizationRequests = customizationRequests;
        Quotations = quotations;
        PhaseDeadlines = phaseDeadlines;
    }

    public RoomPlannerSceneRepository? RoomPlannerScenes { get; }
    public INotificationDispatcher? Notifications { get; }
    public ILogger<ProposalService>? Logger { get; }
    public ICustomizationRequestRepository? CustomizationRequests { get; }
    public IQuotationService? Quotations { get; }
    public IProjectPhaseDeadlineService? PhaseDeadlines { get; }
}
