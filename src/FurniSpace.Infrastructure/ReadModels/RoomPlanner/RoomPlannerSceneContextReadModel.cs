using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Proposals;

namespace FurniSpace.Infrastructure.ReadModels.RoomPlanner;

public sealed class RoomPlannerSceneContextReadModel
{
    public Guid SceneId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public ProposalSceneType? SceneType { get; set; }
    public IReadOnlyList<ProposalSceneAreaReadModel> SceneAreas { get; set; } = [];
    public IReadOnlyList<Guid> ProjectAreaIds => SceneAreas.Select(area => area.ProjectAreaId).ToList();
    public string? MongoSceneId { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
