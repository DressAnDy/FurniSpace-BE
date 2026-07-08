using FurniSpace.Application.Interfaces.Notifications;
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
        ICustomizationRequestRepository? customizationRequests = null)
    {
        RoomPlannerScenes = roomPlannerScenes;
        Notifications = notifications;
        Logger = logger;
        CustomizationRequests = customizationRequests;
    }

    public RoomPlannerSceneRepository? RoomPlannerScenes { get; }
    public INotificationDispatcher? Notifications { get; }
    public ILogger<ProposalService>? Logger { get; }
    public ICustomizationRequestRepository? CustomizationRequests { get; }
}
