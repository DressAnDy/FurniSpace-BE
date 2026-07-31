using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalSceneAreaReadModel
{
    public Guid ProposalSceneAreaId { get; set; }
    public Guid SceneId { get; set; }
    public Guid ProjectAreaId { get; set; }
    public Guid ProjectId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public ProjectAreaType? AreaType { get; set; }
    public int? FloorNumber { get; set; }
    public int SortOrder { get; set; }
    public ProjectAreaStatus? Status { get; set; }
}
