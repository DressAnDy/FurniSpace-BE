using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalSceneContextReadModel
{
    public Guid SceneId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public ProposalSceneType? SceneType { get; set; } = ProposalSceneType.ROOM_PLANNER;
    public IReadOnlyList<ProposalSceneAreaReadModel> SceneAreas { get; set; } = [];
    public IReadOnlyList<Guid> ProjectAreaIds => SceneAreas.Select(area => area.ProjectAreaId).ToList();
    public ProposalStatus? ProposalStatus { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
